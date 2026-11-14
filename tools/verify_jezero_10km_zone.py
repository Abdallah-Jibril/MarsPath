"""Verify staged 10 km tiles against the unchanged local source master crop."""
import os,json,math,shutil
from pathlib import Path
import numpy as np
from osgeo import gdal
os.environ['QT_QPA_PLATFORM']='offscreen'
from qgis.core import Qgis,QgsApplication,QgsProject,QgsRasterLayer,QgsCoordinateReferenceSystem,QgsReferencedRectangle,QgsMapSettings,QgsMapRendererParallelJob
from qgis.PyQt.QtCore import QSize
from qgis.PyQt.QtGui import QColor
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'GIS/OfficialSurfaceSources/JezeroMissionZone10km';STAGE=ROOT/'work/JezeroMissionZone10kmRuntime'
if not (STAGE/'JezeroZone.json').exists():STAGE=ROOT/'MarsPathUnity/Assets/Resources/NASA/MissionZone'
audit=json.loads((OUT/'coverage-audit.json').read_text());definition=json.loads((STAGE/'JezeroZone.json').read_text())
source=gdal.Open(str(OUT/'Jezero10kmNativeDtm.tif'));image=gdal.Open(str(OUT/'Jezero10kmMatchingPhotograph1m.tif'))
coverage=np.zeros((source.RasterYSize,source.RasterXSize),dtype=np.uint8);tiles=[]
lo=audit['displayLow'];hi=audit['displayHigh'];nodata=image.GetRasterBand(1).GetNoDataValue()
for t in audit['tiles']:
    index=t['index'];x,y,w,h=t['nativeWindow'];name='Jezero10kmTile'+str(index).zfill(2)
    native=source.ReadAsArray(x,y,w,h);bundle=np.fromfile(STAGE/(name+'Heights.bytes'),dtype='<f4').reshape(h,w)
    original=image.ReadAsArray(x,y,w,h);stored=gdal.Open(str(STAGE/(name+'OrthoPixels.bytes'))).ReadAsArray()
    expected=np.clip((original.astype(np.float32)-lo)*(255/max(hi-lo,1e-6)),0,255).astype(np.uint8)
    gray=stored if stored.ndim==2 else stored[0]
    alpha=np.full(expected.shape,255,dtype=np.uint8) if stored.ndim==2 else stored[1] if stored.shape[0]==2 else stored[3]
    record=dict(index=index,heightBitwiseEqualToUnchangedMaster=bool(np.array_equal(native,bundle)),
        imageDNMatchesDeclaredStretch=bool(np.array_equal(gray,expected)),
        imageAlphaMatchesOriginal1mDisplayNodata=bool(np.array_equal(alpha,(original!=nodata).astype(np.uint8)*255)),heightDimensionsUnder3072=bool(w<=3072 and h<=3072))
    assert all(v for k,v in record.items() if k!='index'),record
    coverage[y:y+h,x:x+w]+=1;tiles.append(record);print('Verified native tile',index+1,'/16',flush=True)
assert (coverage>0).all()
oracles=json.loads((STAGE/'JezeroZoneElevationReference.json').read_text());assert oracles['source']==definition['dtmSource']
for p in oracles['points']:
    assert definition['west']<p['longitude']<definition['east'] and definition['south']<p['latitude']<definition['north']
proof=dict(allNativeSamplesCovered=bool((coverage>0).all()),maximumOverlappingTiles=int(coverage.max()),tiles=tiles,
    sourceHeightCount=audit['heightSampleCount'],sourceInvalidHeightCount=audit['invalidHeightCount'],
    sourceImageInvalidCount=audit['invalidImageCount'],nativeHeightWasResampled=False,heightGapsFilled=False,imageGapsFilled=False,
    imageSourceSpacingMeters=.25,imageRuntimeDisplaySpacingMeters=1,oracleCount=len(oracles['points']),runtimePayloadBytes=audit['runtimePayloadBytes'])
(OUT/'independent-local-verification.json').write_text(json.dumps(proof,indent=2))

overview=OUT/'Jezero10kmPhotographOverview.png';shutil.copyfile(STAGE/'JezeroZoneOverviewPixels.bytes',overview)
png=gdal.Open(str(overview));g=source.GetGeoTransform()
vrt=gdal.Translate(str(OUT/'Jezero10kmPhotographOverview.vrt'),png,format='VRT')
vrt.SetGeoTransform([g[0],source.RasterXSize/png.RasterXSize,0,g[3],0,-source.RasterYSize/png.RasterYSize]);vrt.SetProjection(source.GetProjection());vrt=None
app=QgsApplication([],False);app.initQgis();project=QgsProject.instance();crs=QgsCoordinateReferenceSystem();crs.createFromWkt(source.GetProjection());project.setCrs(crs);project.setFilePathStorage(Qgis.FilePathType.Relative)
project.setTitle('Jezero 10 km mission zone — native USGS HiRISE TRN master / no height resampling')
height=QgsRasterLayer(str(OUT/'Jezero10kmNativeDtm.tif'),'Original USGS TRN master native 1 m elevations — unchanged')
photo=QgsRasterLayer(str(OUT/'Jezero10kmPhotographOverview.vrt'),'Matching .25 m-source photograph / 1 m ground display / overview 2.58 m')
assert height.isValid() and photo.isValid()
project.addMapLayer(height);project.addMapLayer(photo);project.layerTreeRoot().findLayer(height.id()).setItemVisibilityChecked(False)
extent=photo.extent();extent.scale(1.03);project.viewSettings().setDefaultViewExtent(QgsReferencedRectangle(extent,crs))
project.setFileName(str(OUT/'Jezero10kmReference.qgz'));assert project.write()
settings=QgsMapSettings();settings.setDestinationCrs(crs);settings.setLayers([photo]);settings.setExtent(extent);settings.setOutputSize(QSize(1400,1400));settings.setBackgroundColor(QColor('#0c1820'))
job=QgsMapRendererParallelJob(settings);job.start();job.waitForFinished();job.renderedImage().save(str(OUT/'Jezero10kmOverview.png'))
print(json.dumps(proof,indent=2));print('Portable 10 km reference project and photographic preview saved.',flush=True)
