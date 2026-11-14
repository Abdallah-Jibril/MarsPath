import os,json,math,time
from pathlib import Path
import numpy as np
from osgeo import gdal
gdal.UseExceptions();os.environ['GDAL_HTTP_TIMEOUT']='40';os.environ['GDAL_HTTP_CONNECTTIMEOUT']='15'
os.environ['GDAL_DISABLE_READDIR_ON_OPEN']='EMPTY_DIR';os.environ['CPL_VSIL_CURL_ALLOWED_EXTENSIONS']='.tif'
os.environ['CPL_VSIL_CURL_CACHE_SIZE']='16777216';os.environ['GTIFF_DIRECT_IO']='YES'
root=Path(__file__).resolve().parents[1];out=root/'GIS/OfficialSurfaceSources/JezeroMissionZone10kmAudit';out.mkdir(parents=True,exist_ok=True)
url='https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif'
ds=gdal.Open('/vsicurl/'+url);gt=ds.GetGeoTransform();nodata=ds.GetRasterBand(1).GetNoDataValue();R=3396190
latitude=18.46205517;longitude=77.42472712
center_x=(math.radians(longitude)*R-gt[0])/gt[1];center_y=(math.radians(latitude)*R-gt[3])/gt[5]
w,h=10564,10020;xoff=int(round(center_x-w/2));yoff=int(round(center_y-h/2))
rows=[]
for k,row in enumerate(range(yoff,yoff+h,64)):
    a=ds.ReadAsArray(xoff,row,w,1);valid=np.isfinite(a)&(a!=nodata)&(a>-10000)&(a<30000)
    rows.append(dict(row=row,invalidCount=int(valid.size-valid.sum())))
    if k%24==0:print('Footprint sampled rows',k+1,'/',math.ceil(h/64),flush=True)
points=[]
for lat,lon in [(18.46205517,77.42472712),(18.46402740,77.42277203),(18.46470,77.42535),(18.46820,77.42535)]:
    lat=float(np.float32(lat));lon=float(np.float32(lon));x=(math.radians(lon)*R-gt[0])/gt[1]-.5;y=(math.radians(lat)*R-gt[3])/gt[5]-.5
    i=int(math.floor(x));j=int(math.floor(y));b=ds.ReadAsArray(i,j,2,2).astype(float);tx=x-i;ty=y-j
    height=float((b[0,0]*(1-tx)+b[0,1]*tx)*(1-ty)+(b[1,0]*(1-tx)+b[1,1]*tx)*ty)
    points.append(dict(latitude=lat,longitude=lon,heightMeters=height,nativeStencil=b.tolist()))
report=dict(source=url,sourcePixelWindow=[xoff,yoff,w,h],sourceTransform=list(gt),sampledRowStep=64,sampledRowCount=len(rows),sampledNativeHeightCount=len(rows)*w,
    sampledInvalidHeightCount=sum(r['invalidCount'] for r in rows),rowsWithInvalid=[r for r in rows if r['invalidCount']],sampledRows=rows,sourceCheckPoints=points,
    caveat='Sparse native-row footprint screen establishes candidate viability only; continuous validity requires the entire native candidate to be checked before publication.')
(out/'candidate-footprint-screen.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='sampledRows'},indent=2),flush=True)
