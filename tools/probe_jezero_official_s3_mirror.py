import os,json
from pathlib import Path
from osgeo import gdal
gdal.UseExceptions()
for k,v in dict(GDAL_HTTP_TIMEOUT='25',GDAL_HTTP_CONNECTTIMEOUT='12',GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR',CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif',GTIFF_DIRECT_IO='YES').items():os.environ[k]=v
root=Path(__file__).resolve().parents[1];out=root/'GIS/OfficialSurfaceSources/JezeroMissionZone10kmAudit'
base='https://asc-pds-services.s3.us-west-2.amazonaws.com/mosaic/mars2020_trn/HiRISE/'
names=['JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif','JEZ_hirise_soc_006_orthoMosaic_25cm_Eqc_latTs0_lon0_first.tif']
records=[]
for name in names:
    url=base+name;d=gdal.Open('/vsicurl/'+url);b=d.GetRasterBand(1)
    item=dict(url=url,width=d.RasterXSize,height=d.RasterYSize,transform=list(d.GetGeoTransform()),type=gdal.GetDataTypeName(b.DataType),nodata=b.GetNoDataValue())
    if 'DTM' in name:item['nativeStencilAtCanonicalScarp']=d.ReadAsArray(11960,12272,2,2).tolist()
    records.append(item);print(json.dumps(item),flush=True)
(out/'official-s3-mirror-metadata.json').write_text(json.dumps(records,indent=2))
