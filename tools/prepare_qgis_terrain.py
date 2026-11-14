"""Create a reproducible QGIS terrain inspection project from MarsPath assets.

Run with the Python environment supplied by QGIS, for example:
  "C:/Program Files/QGIS 3.44.15/bin/python-qgis-ltr.bat" tools/prepare_qgis_terrain.py

Native DEM cells are copied exactly. Horn slope and hillshade are derived in
each source's projected metre grid with unit vertical scale. These analysis
rasters are independent of Unity route calculations.
"""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

import numpy as np
from osgeo import gdal, osr
from qgis.PyQt.QtCore import QSize, QVariant
from qgis.PyQt.QtGui import QColor, QFont, QFontDatabase
from qgis.core import (
    Qgis, QgsApplication, QgsBookmark, QgsCoordinateReferenceSystem,
    QgsCoordinateTransform, QgsFeature, QgsField, QgsGeometry, QgsMapRendererParallelJob,
    QgsMapSettings, QgsMarkerSymbol, QgsPalLayerSettings, QgsPointXY, QgsProject,
    QgsRasterLayer, QgsRasterShader, QgsColorRampShader, QgsSingleBandPseudoColorRenderer,
    QgsRectangle, QgsReferencedRectangle, QgsTextFormat, QgsVectorFileWriter,
    QgsVectorLayer, QgsVectorLayerSimpleLabeling,
)


ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "MarsPathUnity/Assets/Resources"
OUTPUT = ROOT / "GIS"
MARS_RADIUS = 3396190.0


def projected_srs(projection):
    result = osr.SpatialReference()
    result.ImportFromProj4(projection)
    return result


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_records():
    for name in ("TrekWestF03", "NewRouteJ01"):
        metadata_path = RESOURCES / "NASA/Stereo" / (name + ".json")
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        source_path = metadata_path.with_name(name + "Heights.bytes")
        values = np.fromfile(source_path, dtype="<f4").reshape(metadata["height"], metadata["width"])
        projection = "+proj=eqc +lat_ts=0 +lat_0=0 +lon_0=0 +a=3396190 +b=3396190 +units=m +no_defs"
        transform = (metadata["west"], metadata["dx"], 0, metadata["north"], 0, metadata["dy"])
        yield name, metadata, source_path, values, np.nan, projection, transform

    catalog = json.loads((RESOURCES / "NASA/HRSC/StripCatalog.json").read_text(encoding="utf-8"))
    for metadata in catalog["strips"]:
        if not metadata["resource"].endswith(("_Team75", "_WestCrater50")):
            continue
        name = Path(metadata["resource"]).name
        source_path = RESOURCES / (metadata["resource"] + ".bytes")
        values = np.fromfile(source_path, dtype=">i2", offset=metadata["dataOffset"])
        values = values.reshape(metadata["height"], metadata["width"]).astype(np.float32)
        radius, longitude = metadata["radius"], metadata["longitude"]
        projection = f"+proj=sinu +lon_0={longitude} +a={radius} +b={radius} +units=m +no_defs"
        step = metadata["spacing"]
        transform = (metadata["west"], step, 0, metadata["north"], 0, -step)
        yield name, metadata, source_path, values, -32768, projection, transform


def small_void_estimates(values, spacing):
    """Match the bounded display estimate policy in MarsDtmVoidEstimates.cs."""
    from scipy.ndimage import binary_dilation, find_objects, label

    invalid = ~np.isfinite(values) | (values <= -10000) | (values >= 30000)
    components, _ = label(invalid, structure=np.ones((3, 3), dtype=np.uint8))
    estimates, mask = values.copy(), np.zeros(values.shape, dtype=np.uint8)
    completed = 0
    for component, bounds in enumerate(find_objects(components), 1):
        if bounds is None:
            continue
        rows, columns = bounds
        if rows.start == 0 or columns.start == 0 or rows.stop == values.shape[0] or columns.stop == values.shape[1]:
            continue
        width, height = columns.stop - columns.start, rows.stop - rows.start
        if math.hypot(width, height) * spacing > 400:
            continue
        local_rows = slice(rows.start - 1, rows.stop + 1)
        local_columns = slice(columns.start - 1, columns.stop + 1)
        component_mask = components[local_rows, local_columns] == component
        count = int(component_mask.sum())
        if count > 512:
            continue
        boundary_mask = binary_dilation(component_mask, structure=np.ones((3, 3)))
        boundary_mask &= ~invalid[local_rows, local_columns]
        boundary_y, boundary_x = np.where(boundary_mask)
        heights = values[local_rows, local_columns][boundary_mask].astype(np.float64)
        if not heights.size:
            continue
        hole_y, hole_x = np.where(component_mask)
        squared_distance = ((hole_y[:, None] - boundary_y) ** 2
                            + (hole_x[:, None] - boundary_x) ** 2)
        weights = 1.0 / squared_distance
        filled = np.clip((weights * heights).sum(axis=1) / weights.sum(axis=1), heights.min(), heights.max())
        global_y = hole_y + local_rows.start
        global_x = hole_x + local_columns.start
        estimates[global_y, global_x] = filled.astype(np.float32)
        mask[global_y, global_x] = 1
        completed += 1
    if not np.array_equal(estimates[mask == 0], values[mask == 0], equal_nan=True):
        raise ValueError("Display estimates changed original finite height samples")
    return estimates, mask, completed


def write_derived(path, values, transform, projection, nodata):
    data_type = gdal.GDT_Byte if values.dtype == np.uint8 else gdal.GDT_Float32
    dataset = gdal.GetDriverByName("GTiff").Create(
        str(path), values.shape[1], values.shape[0], 1, data_type,
        options=["TILED=YES", "COMPRESS=DEFLATE"],
    )
    dataset.SetGeoTransform(transform)
    dataset.SetProjection(projected_srs(projection).ExportToWkt())
    band = dataset.GetRasterBand(1)
    band.SetNoDataValue(nodata)
    band.WriteArray(values)
    dataset = None


def export_native(name, metadata, source_path, values, nodata, projection, transform, include_estimates=False):
    if file_hash(source_path) != metadata["sha256"]:
        raise ValueError(f"Bundled source checksum does not match metadata: {source_path}")
    folder = OUTPUT / name
    folder.mkdir(parents=True, exist_ok=True)
    dem_path = folder / "native-dem.tif"
    source_srs = projected_srs(projection)
    dataset = gdal.GetDriverByName("GTiff").Create(
        str(dem_path), values.shape[1], values.shape[0], 1, gdal.GDT_Float32,
        options=["TILED=YES", "COMPRESS=DEFLATE", "PREDICTOR=3"],
    )
    dataset.SetProjection(source_srs.ExportToWkt())
    dataset.SetGeoTransform(transform)
    dataset.SetMetadata({"SOURCE_URL": metadata.get("source", metadata.get("url", "")),
                         "SOURCE_ID": metadata["id"], "SOURCE_SHA256": metadata["sha256"],
                         "HEIGHT_UNITS": "metre above source Mars areoid", "RESAMPLING": "none"})
    band = dataset.GetRasterBand(1)
    band.SetNoDataValue(float(nodata))
    band.SetUnitType("m")
    band.WriteArray(values)
    dataset = None
    reopened = gdal.Open(str(dem_path))
    if not np.array_equal(reopened.ReadAsArray(), values, equal_nan=True):
        raise ValueError(f"Export altered source cells: {name}")
    reopened = None

    slope_path, shade_path = folder / "slope-degrees.tif", folder / "hillshade.tif"
    options = ["TILED=YES", "COMPRESS=DEFLATE"]
    slope = gdal.DEMProcessing(
        str(slope_path), str(dem_path), "slope", format="GTiff", alg="Horn",
        scale=1.0, slopeFormat="degree", computeEdges=False, creationOptions=options,
    )
    if slope is None:
        raise RuntimeError(f"Slope calculation failed: {name}")
    slope = None
    shade = gdal.DEMProcessing(
        str(shade_path), str(dem_path), "hillshade", format="GTiff", alg="Horn",
        scale=1.0, zFactor=1.0, azimuth=315, altitude=45, computeEdges=False,
        creationOptions=options,
    )
    if shade is None:
        raise RuntimeError(f"Hillshade calculation failed: {name}")
    shade = None
    slope_values = gdal.Open(str(slope_path)).ReadAsArray()
    horn_errors = []
    for row in range(10, values.shape[0] - 10, 127):
        for column in range(10, values.shape[1] - 10, 127):
            patch = values[row - 1:row + 2, column - 1:column + 2].astype(np.float64)
            if not np.isfinite(patch).all() or (patch == nodata).any():
                continue
            dx = ((patch[0, 2] + 2 * patch[1, 2] + patch[2, 2])
                  - (patch[0, 0] + 2 * patch[1, 0] + patch[2, 0])) / (8 * transform[1])
            dy = ((patch[2, 0] + 2 * patch[2, 1] + patch[2, 2])
                  - (patch[0, 0] + 2 * patch[0, 1] + patch[0, 2])) / (8 * abs(transform[5]))
            expected = math.degrees(math.atan(math.hypot(dx, dy)))
            horn_errors.append(abs(float(slope_values[row, column]) - expected))
    # GDAL accumulates the Float32 elevation stencil in single precision.
    # Its cancellation error for kilometre-offset elevations is below .002°.
    if not horn_errors or max(horn_errors) > .002:
        raise ValueError(f"Horn slope verification failed: {name}")
    valid = np.isfinite(values) & (values != nodata) & (values > -10000) & (values < 30000)
    samples = values[valid]
    audit = {
        "name": name, "source": metadata.get("source", metadata.get("url", "")),
        "sourceId": metadata["id"], "sourceSha256": metadata["sha256"],
        "dem": dem_path, "slope": slope_path, "hillshade": shade_path,
        "crs": source_srs.ExportToWkt(), "transform": transform,
        "width": values.shape[1], "height": values.shape[0], "validPixels": int(valid.sum()),
        "minimumMetres": float(samples.min()), "maximumMetres": float(samples.max()),
        "nativeCellsUnchanged": True, "nodata": "NaN" if math.isnan(nodata) else nodata,
        "hornCheckCount": len(horn_errors), "hornMaximumErrorDegrees": max(horn_errors),
    }
    if include_estimates and name in ("TrekWestF03", "NewRouteJ01"):
        estimated, mask, components = small_void_estimates(values, metadata["spacing"])
        estimate_path, mask_path = folder / "display-only-void-estimates.tif", folder / "estimated-void-mask.tif"
        write_derived(estimate_path, estimated, transform, projection, float(nodata))
        write_derived(mask_path, mask, transform, projection, 0)
        audit.update({"visual-estimates": estimate_path, "estimated-mask": mask_path,
                      "estimatedVoidPixels": int(mask.sum()), "estimatedComponents": components,
                      "estimatePolicy": "8-connected enclosed voids, <=512 cells and <=400m bounding-box diagonal. "
                      "All original 8-neighbor boundary values, inverse squared pixel distance, clamped to boundary range. "
                      "Display-only. Estimated cells require exclusion from measured routing grids."})
    return audit


def colorize(layer, minimum, maximum, slope=False):
    ramp = QgsColorRampShader()
    ramp.setColorRampType(QgsColorRampShader.Interpolated)
    colors = [(0, "#173e48"), (.4, "#80634d"), (.7, "#c7aa82"), (1, "#f4ead4")]
    if slope:
        colors = [(0, "#143f45"), (.3, "#5a9d77"), (.5, "#edc565"), (.75, "#f18d41"), (1, "#c52c29")]
    ramp.setColorRampItemList([
        QgsColorRampShader.ColorRampItem(minimum + (maximum - minimum) * position, QColor(color))
        for position, color in colors
    ])
    shader = QgsRasterShader()
    shader.setRasterShaderFunction(ramp)
    renderer = QgsSingleBandPseudoColorRenderer(layer.dataProvider(), 1, shader)
    renderer.setClassificationMin(minimum)
    renderer.setClassificationMax(maximum)
    layer.setRenderer(renderer)


def add_points(project, crs):
    layer = QgsVectorLayer("Point?crs=" + crs.toWkt(), "Selected geographic points", "memory")
    provider = layer.dataProvider()
    provider.addAttributes([QgsField("label", QVariant.String), QgsField("latitude", QVariant.Double),
                            QgsField("longitude", QVariant.Double)])
    layer.updateFields()
    records = [("Recent route start", 24.67645, 58.36556),
               ("Recent route destination", 27.68045, 53.42641),
               ("NASA Trek west crater", 21.010789, -22.531535)]
    for label, latitude, longitude in records:
        feature = QgsFeature(layer.fields())
        feature.setAttributes([label, latitude, longitude])
        point = QgsPointXY(MARS_RADIUS * math.radians(longitude), MARS_RADIUS * math.radians(latitude))
        feature.setGeometry(QgsGeometry.fromPointXY(point))
        provider.addFeatures([feature])
    layer.updateExtents()
    settings = QgsVectorFileWriter.SaveVectorOptions()
    settings.driverName, settings.layerName = "GPKG", "selected_points"
    target = OUTPUT / "selected-points.gpkg"
    if target.exists():
        target.unlink()
    error, message, _, _ = QgsVectorFileWriter.writeAsVectorFormatV3(
        layer, str(target), project.transformContext(), settings)
    if error != QgsVectorFileWriter.NoError:
        raise RuntimeError(message)
    persistent = QgsVectorLayer(str(target) + "|layername=selected_points", layer.name(), "ogr")
    persistent.renderer().setSymbol(QgsMarkerSymbol.createSimple(
        {"name": "circle", "color": "36,225,245,255", "outline_color": "5,25,29,255", "size": "3.5"}))
    labels = QgsPalLayerSettings()
    labels.fieldName = "label"
    text = QgsTextFormat()
    text.setFont(QFont("Arial"))
    text.setSize(10)
    text.setColor(QColor("#1a3139"))
    labels.setFormat(text)
    persistent.setLabeling(QgsVectorLayerSimpleLabeling(labels))
    persistent.setLabelsEnabled(True)
    project.addMapLayer(persistent)
    return persistent


def add_bookmark(project, name, extent, crs):
    bookmark = QgsBookmark()
    bookmark.setName(name)
    bookmark.setGroup("MarsPath official terrain")
    bookmark.setExtent(QgsReferencedRectangle(extent, crs))
    project.bookmarkManager().addBookmark(bookmark)


def render_map(project, layers, extent, path):
    settings = QgsMapSettings()
    settings.setDestinationCrs(project.crs())
    settings.setLayers(layers)
    settings.setExtent(extent)
    settings.setOutputSize(QSize(1600, 1000))
    settings.setBackgroundColor(QColor("#f0ebe0"))
    job = QgsMapRendererParallelJob(settings)
    job.start()
    job.waitForFinished()
    if not job.renderedImage().save(str(path)):
        raise RuntimeError(f"Could not render QGIS overview: {path}")


def main():
    arguments = argparse.ArgumentParser(description=__doc__)
    arguments.add_argument("--include-void-estimates", action="store_true",
                           help="Add optional display-only gap estimates and their explicit masks")
    options = arguments.parse_args()
    gdal.UseExceptions()
    application = QgsApplication([], False)
    application.initQgis()
    # The offscreen Qt platform can start with an empty font database on Windows.
    font_path = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/arial.ttf"
    if font_path.exists():
        QFontDatabase.addApplicationFont(str(font_path))
    OUTPUT.mkdir(exist_ok=True)
    project = QgsProject.instance()
    project.setTitle("MarsPath native Mars DEM, Horn slope and hillshade")
    project.setFilePathStorage(Qgis.FilePathType.Relative)
    geographic_projection = "+proj=eqc +lat_ts=0 +lat_0=0 +lon_0=0 +a=3396190 +b=3396190 +units=m +no_defs"
    project_crs = QgsCoordinateReferenceSystem.fromProj(geographic_projection)
    project.setCrs(project_crs)
    layers, audits = {}, []
    for record in source_records():
        name = record[0]
        audit = export_native(*record, include_estimates=options.include_void_estimates)
        group = project.layerTreeRoot().addGroup(name)
        group.setExpanded(False)
        source_layers = {}
        kinds = ["dem", "hillshade", "slope"]
        if "visual-estimates" in audit:
            kinds.extend(["visual-estimates", "estimated-mask"])
        for kind in kinds:
            layer = QgsRasterLayer(str(audit[kind]), name + " / " + kind)
            if not layer.isValid():
                raise RuntimeError(f"QGIS could not open {audit[kind]}")
            if kind in ("dem", "visual-estimates"):
                minimum = audit["minimumMetres"] if name in ("TrekWestF03", "H5271_WestCrater50") else -3600
                maximum = audit["maximumMetres"] if name in ("TrekWestF03", "H5271_WestCrater50") else 2420
                colorize(layer, minimum, maximum)
            elif kind == "slope":
                colorize(layer, 0, 50, slope=True)
            elif kind == "hillshade":
                layer.setOpacity(.48)
            project.addMapLayer(layer, False)
            node = group.insertLayer(0, layer)
            node.setItemVisibilityChecked(kind in ("dem", "hillshade"))
            source_layers[kind] = layer
        layers[name] = source_layers
        audits.append(audit)
        print(f"QGIS_NATIVE_VERIFIED {name}: {audit['validPixels']} cells, "
              f"{audit['minimumMetres']:.2f} to {audit['maximumMetres']:.2f} m", flush=True)

    points = add_points(project, project_crs)
    crater_extent = layers["TrekWestF03"]["dem"].extent()
    recent_extent = QgsRectangle()
    for name, source_layers in layers.items():
        if name in ("TrekWestF03", "H5271_WestCrater50"):
            continue
        layer = source_layers["dem"]
        transform = QgsCoordinateTransform(layer.crs(), project_crs, project)
        recent_extent.combineExtentWith(transform.transformBoundingBox(layer.extent()))
    add_bookmark(project, "NASA Trek west crater / native CTX 20.06 m", crater_extent, project_crs)
    add_bookmark(project, "Recent route corridor / native HRSC 75 m and CTX 20 m", recent_extent, project_crs)
    project.viewSettings().setDefaultViewExtent(QgsReferencedRectangle(crater_extent, project_crs))
    project_path = OUTPUT / "MarsPathTerrain.qgz"
    if not project.write(str(project_path)):
        raise RuntimeError("Could not save the QGIS project")
    crater_layers = [points, layers["TrekWestF03"]["hillshade"], layers["TrekWestF03"]["dem"]]
    if "H5271_WestCrater50" in layers:
        crater_layers.extend([layers["H5271_WestCrater50"]["hillshade"], layers["H5271_WestCrater50"]["dem"]])
    render_map(project, crater_layers, crater_extent, OUTPUT / "west-crater-qgis.png")
    recent_layers = [points]
    for name, source_layers in layers.items():
        if name not in ("TrekWestF03", "H5271_WestCrater50"):
            recent_layers.extend([source_layers["hillshade"], source_layers["dem"]])
    render_map(project, recent_layers, recent_extent, OUTPUT / "recent-corridor-qgis.png")
    for audit in audits:
        for key in ("dem", "slope", "hillshade", "visual-estimates", "estimated-mask"):
            if key not in audit:
                continue
            path = audit[key]
            audit[key] = str(path.relative_to(OUTPUT))
            audit[key + "Sha256"] = file_hash(path)
    manifest = {"qgisVersion": Qgis.QGIS_VERSION, "gdalVersion": gdal.VersionInfo(),
                "processing": "Exact native DEM cells, original CRS and nodata. Horn slope in degrees, "
                "vertical/horizontal metre ratio 1. Hillshade z factor 1, azimuth 315, altitude 45. "
                "Slope describes each native projected grid. QGIS display reprojects layers on the fly.",
                "datasets": audits}
    (OUTPUT / "terrain-audit.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("QGIS_PROJECT_READY " + str(project_path), flush=True)
    project.clear()
    application.exitQgis()


if __name__ == "__main__":
    main()
