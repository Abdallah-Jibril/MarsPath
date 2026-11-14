"""Stage native 0.25 m HiRISE imagery around the Belva or western delta routes."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path

import numpy as np
from osgeo import gdal


ROOT = Path(__file__).resolve().parents[1]
ZONE = ROOT / 'GIS/OfficialSurfaceSources/JezeroMissionZone10km'
OUTPUT = ROOT / 'work/BelvaNativeImage'
SOURCE_GRID = 0.25
MIRROR = 'https://asc-pds-services.s3.us-west-2.amazonaws.com/mosaic/mars2020_trn/HiRISE/'
RADIUS = 3396190.0
SIDE = 3072
BELVA = [
    ('JezeroBelvaNative', 18.4810, 77.3785),
    ('JezeroBelvaSouthNative', 18.4758, 77.3776),
]
DELTA = [
    (f'JezeroDeltaNative{index:02}', 18.44470 + .02250 * index / 4, 77.45100 - .02120 * index / 4)
    for index in range(5)
]


def stage(source, reference, contrast, asset, latitude, longitude):
    transform = source.GetGeoTransform()
    if tuple(transform) != tuple(reference['transform']) or transform[1] != SOURCE_GRID:
        raise ValueError('Source transform no longer matches the registered Jezero mosaic')
    x = round((math.radians(longitude) * RADIUS - transform[0]) / SOURCE_GRID) - SIDE // 2
    y = round((transform[3] - math.radians(latitude) * RADIUS) / SOURCE_GRID) - SIDE // 2
    raster = source.GetRasterBand(1)
    pixels = raster.ReadAsArray(x, y, SIDE, SIDE)
    if pixels.shape != (SIDE, SIDE) or np.any(pixels == raster.GetNoDataValue()):
        raise ValueError(f'{asset} photograph has missing source samples')

    low, high = contrast['displayLow'], contrast['displayHigh']
    display = np.clip((pixels.astype(np.float32) - low) * (255 / (high - low)), 0, 255).astype(np.uint8)
    image = gdal.GetDriverByName('MEM').Create('', SIDE, SIDE, 1, gdal.GDT_Byte)
    band = image.GetRasterBand(1)
    band.SetColorInterpretation(gdal.GCI_GrayIndex)
    band.WriteArray(display)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    payload = OUTPUT / (asset + 'OrthoPixels.bytes')
    gdal.GetDriverByName('PNG').CreateCopy(str(payload), image)
    # Audit the complete PNG against the contrast-mapped original DN samples.
    if not np.array_equal(gdal.Open(str(payload)).ReadAsArray(), display):
        raise ValueError('Encoded image does not match the original sample grid')

    west = math.degrees((transform[0] + x * SOURCE_GRID) / RADIUS)
    north = math.degrees((transform[3] - y * SOURCE_GRID) / RADIUS)
    east = math.degrees((transform[0] + (x + SIDE) * SOURCE_GRID) / RADIUS)
    south = math.degrees((transform[3] - (y + SIDE) * SOURCE_GRID) / RADIUS)
    metadata = dict(key='Bundled' + asset, source=reference['source'], kind='HiRISE',
                    resource='NASA/MissionZone/' + asset + 'OrthoPixels', spacing=SOURCE_GRID,
                    nativeResolution=True, west=west, south=south, east=east, north=north,
                    rect=dict(x=west / 360 + .5, y=south / 180 + .5,
                              z=(east - west) / 360, w=(north - south) / 180),
                    nativeWindow=[x, y, SIDE, SIDE], validSourceSamples=int(pixels.size),
                    sourceDnSha256=hashlib.sha256(pixels.tobytes()).hexdigest(),
                    payloadSha256=hashlib.sha256(payload.read_bytes()).hexdigest(),
                    displayLow=low, displayHigh=high)
    (OUTPUT / (asset + 'Image.json')).write_text(json.dumps(metadata, indent=2) + '\n', encoding='utf-8')
    print(f'{asset}: {pixels.size} source samples at {SOURCE_GRID} m; {payload.stat().st_size} bytes', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--delta-route', action='store_true', help='Stage western delta route tiles instead of Belva.')
    args = parser.parse_args()
    gdal.UseExceptions()
    os.environ.update(GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR', GDAL_HTTP_CONNECTTIMEOUT='15',
                      GDAL_HTTP_TIMEOUT='40', GDAL_HTTP_MAX_RETRY='2',
                      CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif')
    reference = json.loads((ZONE / 'original-master-image-metadata.json').read_text())
    contrast = json.loads((ZONE / 'coverage-audit.json').read_text())
    transport = reference.get('transportUrl', MIRROR + reference['source'].rsplit('/', 1)[-1])
    source = gdal.Open('/vsicurl/' + transport)
    for asset, latitude, longitude in DELTA if args.delta_route else BELVA:
        stage(source, reference, contrast, asset, latitude, longitude)


if __name__ == '__main__':
    main()
