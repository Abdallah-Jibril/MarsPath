import os,json
from pathlib import Path
from osgeo import gdal
gdal.UseExceptions()
os.environ['GDAL_HTTP_TIMEOUT']='25';os.environ['GDAL_HTTP_CONNECTTIMEOUT']='12'
os.environ['GDAL_DISABLE_READDIR_ON_OPEN']='EMPTY_DIR';os.environ['CPL_VSIL_CURL_ALLOWED_EXTENSIONS']='.tif'
out=Path(__file__).resolve().parents[1]/'GIS/OfficialSurfaceSources/JezeroMissionZone10kmAudit';out.mkdir(parents=True,exist_ok=True)
base='https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/'
urls=[base+'JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif',base+'JEZ_hirise_soc_006_orthoMosaic_25cm_Eqc_latTs0_lon0_first.tif']
result=[]
for url in urls:
    try:
        ds=gdal.Open('/vsicurl/'+url);b=ds.GetRasterBand(1)
        info=dict(url=url,width=ds.RasterXSize,height=ds.RasterYSize,bands=ds.RasterCount,transform=list(ds.GetGeoTransform()),projection=ds.GetProjection(),bandType=gdal.GetDataTypeName(b.DataType),nodata=b.GetNoDataValue(),blockSize=b.GetBlockSize(),overviews=[dict(width=b.GetOverview(i).XSize,height=b.GetOverview(i).YSize) for i in range(b.GetOverviewCount())])
    except Exception as e:info=dict(url=url,error=str(e))
    result.append(info);print(json.dumps(info),flush=True)
(out/'master-source-metadata-probe.json').write_text(json.dumps(result,indent=2))
