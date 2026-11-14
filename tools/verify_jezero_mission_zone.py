"""Independent local verification and portable QGIS preview of the prepared zone."""
import os,json,hashlib,shutil,math
from pathlib import Path
import numpy as np
from osgeo import gdal
os.environ['QT_QPA_PLATFORM']='offscreen'
from qgis.core import (Qgis,QgsApplication,QgsProject,QgsRasterLayer,QgsCoordinateReferenceSystem,QgsRectangle,QgsReferencedRectangle,QgsMapSettings,QgsMapRendererParallelJob)
from qgis.PyQt.QtCore import QSize
from qgis.PyQt.QtGui import QColor,QFontDatabase

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'GIS/OfficialSurfaceSources/JezeroMissionZone'
RES=ROOT/'MarsPathUnity/Assets/Resources/NASA/MissionZone'
audit=json.loads((OUT/'coverage-audit.json').read_text());definition=json.loads((RES/'JezeroZone.json').read_text())
source=gdal.Open(str(OUT/'JezeroMissionZoneNativeDtm.tif'));native=source.ReadAsArray()
coverage=np.zeros(native.shape,dtype=np.uint8)
tiles=[]
lo=audit['displayLow'];hi=audit['displayHigh']
image=gdal.Open(str(OUT/'JezeroMissionZoneNativeRedC.tif')).ReadAsArray()
nodata=audit['imageSource']['nodata']
for t in audit['tiles']:
    i=t['index'];x,y,w,h=t['localNativeWindow']
    values=np.fromfile(RES/('JezeroZoneTile'+str(i)+'Heights.bytes'),dtype='<f4').reshape(h,w)
    height_equal=np.array_equal(values,native[y:y+h,x:x+w])
    coverage[y:y+h,x:x+w]+=1
    ix,iy,iw,ih=t['imageNativeWindowWithinMainCrop']
    photographed=image[iy:iy+ih,ix:ix+iw]
    stored=gdal.Open(str(RES/('JezeroZoneTile'+str(i)+'OrthoPixels.bytes'))).ReadAsArray()
    expected=np.clip((photographed.astype(np.float32)-lo)*(255/(hi-lo)),0,255).astype(np.uint8)
    colours_equal=all(np.array_equal(stored[k],expected) for k in range(3))
    alpha_equal=np.array_equal(stored[3],(photographed!=nodata).astype(np.uint8)*255)
    tiles.append(dict(index=i,heightBitwiseEqualToOriginal=bool(height_equal),imageDisplayDNExactlyEqualToDeclaredStretch=bool(colours_equal),imageNodataAlphaExact=bool(alpha_equal),heightDimensionsWithin3072=bool(w<=3072 and h<=3072)))
old_info=json.loads((ROOT/'MarsPathUnity/Assets/Resources/NASA/Stereo/JezeroHiRise.json').read_text())
old=np.fromfile(ROOT/'MarsPathUnity/Assets/Resources/NASA/Stereo/JezeroHiRiseHeights.bytes',dtype='<f4').reshape(old_info['height'],old_info['width'])
# Old crop starts at source2048,4096. New crop starts at1024,2048.
old_equal=np.array_equal(old,native[2048:4096,1024:3072])
proof=dict(allTilesCoverNativeRectangle=bool((coverage>0).all()),minimumOverlapPixelCoverageCount=int(coverage.min()),maximumOverlapPixelCoverageCount=int(coverage.max()),
    olderIndependentlyVerified4194304ValueCropBitwiseMatches=bool(old_equal),tiles=tiles,
    originalHeightValidSampleCount=int(np.count_nonzero(np.isfinite(native)&(native>-10000)&(native<30000))),
    originalHeightSampleCount=int(native.size),originalImageInvalidSampleCount=int(np.count_nonzero(image==nodata)),
    terrainWasResampled=False,heightGapsFilled=False,imageGapsFilled=False)
assert proof['allTilesCoverNativeRectangle'] and old_equal
assert all(all(v for k,v in t.items() if k!='index') for t in tiles)
(OUT/'independent-local-verification.json').write_text(json.dumps(proof,indent=2))

# The map overview is a labelled photographic display reduction; the separate
# source and ground image tiles above retain native sample locations.
overview_path=OUT/'JezeroMissionZonePhotographOverview.png'
shutil.copyfile(RES/'JezeroZoneOverviewPixels.bytes',overview_path)
png=gdal.Open(str(overview_path));gt=source.GetGeoTransform();R=3396190
west=math.radians(definition['imageWest'])*R;east=math.radians(definition['imageEast'])*R
north=math.radians(definition['imageNorth'])*R;south=math.radians(definition['imageSouth'])*R
vrt=gdal.Translate(str(OUT/'JezeroMissionZonePhotographOverview.vrt'),png,format='VRT')
vrt.SetGeoTransform([west,(east-west)/png.RasterXSize,0,north,0,(south-north)/png.RasterYSize]);vrt.SetProjection(source.GetProjection());vrt=None

app=QgsApplication([],False);app.initQgis();QFontDatabase.addApplicationFont('C:/Windows/Fonts/arial.ttf')
project=QgsProject.instance();crs=QgsCoordinateReferenceSystem();crs.createFromWkt(source.GetProjection());project.setCrs(crs);project.setFilePathStorage(Qgis.FilePathType.Relative)
project.setTitle('Jezero mission zone — verified native HiRISE 1.016553 m terrain / photographic overview')
photo=QgsRasterLayer(str(OUT/'JezeroMissionZonePhotographOverview.vrt'),'Matching HiRISE RED_C photograph / 1.489 m overview / 1.016553 m native source')
dtm=QgsRasterLayer(str(OUT/'JezeroMissionZoneNativeDtm.tif'),'Unresampled measured HiRISE elevations / 25,344,000 valid native samples')
mask=QgsRasterLayer(str(OUT/'JezeroMissionZoneImageValidity.tif'),'Original matching image validity — 1 valid / 0 original source nodata')
assert photo.isValid() and dtm.isValid() and mask.isValid()
for layer in (dtm,mask,photo):project.addMapLayer(layer)
for layer in (dtm,mask):project.layerTreeRoot().findLayer(layer.id()).setItemVisibilityChecked(False)
extent=photo.extent();extent.scale(1.03);project.viewSettings().setDefaultViewExtent(QgsReferencedRectangle(extent,crs))
project.setFileName(str(OUT/'JezeroMissionZoneReference.qgz'));assert project.write()
settings=QgsMapSettings();settings.setDestinationCrs(crs);settings.setLayers([photo]);settings.setExtent(extent);settings.setOutputSize(QSize(1000,1400));settings.setBackgroundColor(QColor('#0c1920'))
job=QgsMapRendererParallelJob(settings);job.start();job.waitForFinished();job.renderedImage().save(str(OUT/'JezeroMissionZoneOverview.png'))
print(json.dumps(proof,indent=2));print('Portable QGIS reference and native photo overview saved.',flush=True)
