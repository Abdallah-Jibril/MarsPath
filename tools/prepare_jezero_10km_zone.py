"""Prepare the authorized 10 km zone from the co-registered USGS TRN master.

--source-only downloads and audits the bounded source crops, without touching
the application's Resources. Default execution stages runtime assets under
work/JezeroMissionZone10kmRuntime only; publishing is a separate root action.
"""
import os,sys,json,math,hashlib,datetime,time
from pathlib import Path
import numpy as np
from osgeo import gdal
gdal.UseExceptions()
for key,value in dict(GDAL_HTTP_TIMEOUT='90',GDAL_HTTP_CONNECTTIMEOUT='15',GDAL_HTTP_MAX_RETRY='9',GDAL_HTTP_RETRY_DELAY='2',GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR',CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif',CPL_VSIL_CURL_CACHE_SIZE='33554432',GTIFF_DIRECT_IO='YES',GDAL_HTTP_MULTIRANGE='PARALLEL').items():os.environ[key]=value
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'GIS/OfficialSurfaceSources/JezeroMissionZone10km';OUT.mkdir(parents=True,exist_ok=True)
STAGE=ROOT/'work/JezeroMissionZone10kmRuntime';STAGE.mkdir(parents=True,exist_ok=True)
BASE='https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/'
DEM=BASE+'JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif'
IMAGE=BASE+'JEZ_hirise_soc_006_orthoMosaic_25cm_Eqc_latTs0_lon0_first.tif'
MIRROR_BASE='https://asc-pds-services.s3.us-west-2.amazonaws.com/mosaic/mars2020_trn/HiRISE/'
WINDOW=[6679,6875,10564,10020]
R=3396190.0;PREFIX='NASA/MissionZone/'
def write_json(p,v):p.write_text(json.dumps(v,indent=2),encoding='utf-8')
def sha(p):
    digest=hashlib.sha256()
    with p.open('rb') as f:
        for chunk in iter(lambda:f.read(8*1024*1024),b''):digest.update(chunk)
    return digest.hexdigest()
def bounds(gt,w,h):return [math.degrees(gt[0]/R),math.degrees((gt[3]+h*gt[5])/R),math.degrees((gt[0]+w*gt[1])/R),math.degrees(gt[3]/R)]
def meta(d,url):
    b=d.GetRasterBand(1)
    return dict(source=url,width=d.RasterXSize,height=d.RasterYSize,transform=list(d.GetGeoTransform()),projection=d.GetProjection(),type=gdal.GetDataTypeName(b.DataType),nodata=b.GetNoDataValue(),blockSize=b.GetBlockSize())
def progress(label):
    previous=[-1]
    def cb(p,m,data):
        n=int(p*20)
        if n>previous[0]:print(label+': '+str(n*5)+'%',flush=True);previous[0]=n
        return 1
    return cb
def completed_checkpoint(path,photo=False):
    cp=path.with_suffix('.checkpoint.json')
    if not path.exists() or not cp.exists():return None
    record=json.loads(cp.read_text())
    if record.get('window')!=WINDOW or record.get('source')!=(IMAGE if photo else DEM):return None
    data=gdal.Open(str(path))
    if data.RasterXSize!=WINDOW[2] or data.RasterYSize!=WINDOW[3]:return None
    for y in range(0,WINDOW[3],512):
        stripe=record.get('stripes',{}).get(str(y));rows=min(512,WINDOW[3]-y)
        if stripe is None or stripe.get('rows')!=rows:return None
        actual=data.ReadAsArray(0,y,WINDOW[2],rows)
        if hashlib.sha256(actual.tobytes(order='C')).hexdigest()!=stripe['sha256']:return None
    return data
def checkpoint_crop(path,source,photo=False):
    checkpoint_path=path.with_suffix('.checkpoint.json')
    checkpoint=json.loads(checkpoint_path.read_text()) if checkpoint_path.exists() else {}
    reuse=path.exists() and checkpoint.get('window')==WINDOW and checkpoint.get('source')==(IMAGE if photo else DEM)
    if reuse:
        output=gdal.Open(str(path),gdal.GA_Update)
    else:
        output=gdal.GetDriverByName('GTiff').Create(str(path),WINDOW[2],WINDOW[3],1,gdal.GDT_Byte if photo else gdal.GDT_Float32,
            options=['TILED=YES','COMPRESS=DEFLATE','PREDICTOR='+('2' if photo else '3'),'NUM_THREADS=2'])
        origin=source.GetGeoTransform();factor=4 if photo else 1
        output.SetGeoTransform([origin[0]+WINDOW[0]*factor*origin[1],1,0,origin[3]+WINDOW[1]*factor*origin[5],0,-1])
        output.SetProjection(source.GetProjection());output.GetRasterBand(1).SetNoDataValue(source.GetRasterBand(1).GetNoDataValue())
        checkpoint=dict(source=IMAGE if photo else DEM,window=WINDOW,photoDecimation=photo,stripes={})
    for y in range(0,WINDOW[3],512):
        rows=min(512,WINDOW[3]-y);key=str(y);previous=checkpoint['stripes'].get(key)
        if previous:
            preserved=output.ReadAsArray(0,y,WINDOW[2],rows)
            if hashlib.sha256(preserved.tobytes(order='C')).hexdigest()==previous['sha256']:continue
        factor=4 if photo else 1
        for attempt in range(4):
            try:
                values=source.ReadAsArray(WINDOW[0]*factor,(WINDOW[1]+y)*factor,WINDOW[2]*factor,rows*factor,
                    buf_xsize=WINDOW[2],buf_ysize=rows,resample_alg=gdal.GRIORA_NearestNeighbour)
                break
            except Exception as e:
                if attempt==3:raise
                print('Retry native stripe',y,str(e),flush=True);gdal.VSICurlClearCache();time.sleep(2+attempt*2)
        output.GetRasterBand(1).WriteArray(values,0,y);output.FlushCache()
        # Close the writer before committing the checkpoint: this also flushes
        # TIFF directory/strile metadata and Windows file-length metadata.
        output=None;output=gdal.Open(str(path),gdal.GA_Update)
        checkpoint['stripes'][key]=dict(rows=rows,sha256=hashlib.sha256(values.tobytes(order='C')).hexdigest())
        temporary=checkpoint_path.with_suffix('.checkpoint.tmp');write_json(temporary,checkpoint);os.replace(temporary,checkpoint_path)
        print(('Matching photo' if photo else 'Native unchanged heights')+' checkpoint: '+str(round((y+rows)/WINDOW[3]*100))+'%',flush=True)
    output.FlushCache();return output
def native_crop():
    path=OUT/'Jezero10kmNativeDtm.tif'
    complete=completed_checkpoint(path,False)
    if complete is not None:return complete
    previous=json.loads((OUT/'coverage-audit.json').read_text()) if (OUT/'coverage-audit.json').exists() else {}
    if path.exists() and previous.get('sourceNativeWindow')==WINDOW and previous.get('nativeHeightCropSha256')==sha(path):
        cached=gdal.Open(str(path))
        if cached.RasterXSize==WINDOW[2] and cached.RasterYSize==WINDOW[3]:return cached
    transport=MIRROR_BASE+DEM.rsplit('/',1)[1];source=gdal.Open('/vsicurl/'+transport)
    info=meta(source,DEM);info['transportUrl']=transport;write_json(OUT/'original-master-dtm-metadata.json',info)
    return checkpoint_crop(path,source,False)
def photographic_crop():
    path=OUT/'Jezero10kmMatchingPhotograph1m.tif'
    complete=completed_checkpoint(path,True)
    if complete is not None:return complete
    previous=json.loads((OUT/'coverage-audit.json').read_text()) if (OUT/'coverage-audit.json').exists() else {}
    if path.exists() and previous.get('sourceNativeWindow')==WINDOW and previous.get('imageCropSha256')==sha(path):
        cached=gdal.Open(str(path))
        if cached.RasterXSize==WINDOW[2] and cached.RasterYSize==WINDOW[3]:return cached
    transport=MIRROR_BASE+IMAGE.rsplit('/',1)[1];source=gdal.Open('/vsicurl/'+transport)
    info=meta(source,IMAGE);info['transportUrl']=transport;write_json(OUT/'original-master-image-metadata.json',info)
    # Nearest 4:1 photographic decimation reads one source row per metre.
    # Elevations are not involved or resampled. Original source nodata is kept.
    return checkpoint_crop(path,source,True)
def png(path,pixels,mask,lo,hi):
    h,w=pixels.shape
    # This is a monochrome RED product. PNG grayscale losslessly expands to
    # identical RGB in the texture decoder, avoiding three stored copies of DN.
    opaque=bool(mask.all());mem=gdal.GetDriverByName('MEM').Create('',w,h,1 if opaque else 2,gdal.GDT_Byte)
    display=np.clip((pixels.astype(np.float32)-lo)*(255/max(hi-lo,1e-6)),0,255).astype(np.uint8)
    mem.GetRasterBand(1).WriteArray(display);mem.GetRasterBand(1).SetColorInterpretation(gdal.GCI_GrayIndex)
    if not opaque:
        mem.GetRasterBand(2).WriteArray(mask.astype(np.uint8)*255);mem.GetRasterBand(2).SetColorInterpretation(gdal.GCI_AlphaBand)
    gdal.GetDriverByName('PNG').CreateCopy(str(path),mem)

dtm=native_crop();dtm.FlushCache();dtm=None;dtm=gdal.Open(str(OUT/'Jezero10kmNativeDtm.tif'));gt=dtm.GetGeoTransform();nodata=dtm.GetRasterBand(1).GetNoDataValue()
invalid=0;count=0;minimum=float('inf');maximum=float('-inf');bad=[]
for y in range(0,dtm.RasterYSize,256):
    a=dtm.ReadAsArray(0,y,dtm.RasterXSize,min(256,dtm.RasterYSize-y));v=np.isfinite(a)&(a!=nodata)&(a>-10000)&(a<30000)
    invalid+=int(v.size-v.sum());count+=v.size
    if not v.all():
        yy,xx=np.where(~v);bad.extend([(int(x),int(y+r)) for x,r in zip(xx[:10],yy[:10])])
    if v.any():minimum=min(minimum,float(a[v].min()));maximum=max(maximum,float(a[v].max()))
audit=dict(retrievedUTC=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=DEM,sourceNativeWindow=WINDOW,sourceGridSpacingMeters=1.0,
    heightTransform=list(gt),projection=dtm.GetProjection(),heightBboxWestSouthEastNorth=bounds(gt,dtm.RasterXSize,dtm.RasterYSize),
    heightWidth=dtm.RasterXSize,heightHeight=dtm.RasterYSize,validHeightCount=count-invalid,heightSampleCount=count,invalidHeightCount=invalid,
    invalidPixelExamples=bad[:20],minimumMeters=minimum,maximumMeters=maximum,nativeHeightCropSha256=sha(OUT/'Jezero10kmNativeDtm.tif'),heightResampled=False,
    producerProduct='USGS Mars 2020 Terrain Relative Navigation HiRISE DTM mosaic, original native 1 m grid, MOLA topography/DeltaGeoid/blend40 release',
    caveat='This master is a different controlled/blended producer product from individual HiRISE strips; original strip heights must not be assumed identical or mixed across this mission frame.')
write_json(OUT/'coverage-audit.json',audit);print('FULL HEIGHT AUDIT '+json.dumps(audit),flush=True)
if invalid:raise RuntimeError('Native candidate contains source nodata; do not publish or fill the requested rectangle')

image=photographic_crop();image.FlushCache();image=None;image=gdal.Open(str(OUT/'Jezero10kmMatchingPhotograph1m.tif'));image_nodata=image.GetRasterBand(1).GetNoDataValue();image_invalid=0;image_count=0
for y in range(0,image.RasterYSize,512):
    a=image.ReadAsArray(0,y,image.RasterXSize,min(512,image.RasterYSize-y));v=(a!=image_nodata);image_invalid+=int(v.size-v.sum());image_count+=v.size
audit.update(imageSource=IMAGE,imageSourceNativeSpacingMeters=.25,imageRuntimeSpacingMeters=1.0,imageResampling='Nearest-neighbor 4:1 photographic decimation; no elevation resampling',
    imageTransform=list(image.GetGeoTransform()),imageWidth=image.RasterXSize,imageHeight=image.RasterYSize,invalidImageCount=image_invalid,imageSampleCount=image_count,validImageFraction=1-image_invalid/image_count,
    imageCropSha256=sha(OUT/'Jezero10kmMatchingPhotograph1m.tif'),imageNodata=image_nodata,imagePNGEncoding='Lossless PNG grayscale; opaque alpha is implicit for entirely source-valid tiles, explicit gray-alpha if a tile has source nodata.')
write_json(OUT/'coverage-audit.json',audit);print('MATCHING PHOTO AUDIT '+json.dumps({k:v for k,v in audit.items() if k.startswith('image') or k=='validImageFraction'}),flush=True)
if '--source-only' in sys.argv:print('Source-only phase complete; application resources untouched.',flush=True);sys.exit(0)

heights=dtm.ReadAsArray();pixels=image.ReadAsArray();valid_image=pixels!=image_nodata
lo,hi=np.percentile(pixels[valid_image],[.5,99.5]);manifest=dict(retrieved=audit['retrievedUTC'],source='Official USGS co-registered Jezero TRN master native 1 m height / matched .25 m photo decimated to 1 m',windows=[],images=[])
tw=(WINDOW[2]+3*128)//4;th=(WINDOW[3]+3*128)//4
reports=[];runtime_files=[]
for row in range(4):
    for col in range(4):
        index=row*4+col;x=col*(tw-128);y=row*(th-128);tile=heights[y:y+th,x:x+tw]
        if tile.shape!=(th,tw):raise RuntimeError('Native tile partition is incomplete')
        name='Jezero10kmTile'+str(index).zfill(2);tg=[gt[0]+x,1,0,gt[3]-y,0,-1];bb=bounds(tg,tw,th)
        height_file=STAGE/(name+'Heights.bytes');height_file.write_bytes(tile.astype('<f4',copy=False).tobytes(order='C'))
        height_meta=STAGE/(name+'Height.json');write_json(height_meta,dict(id='JEZ_hirise_soc_006_TRN_ZONE_'+str(index),source=DEM,kind='HiRISE',width=tw,height=th,west=tg[0],north=tg[3],dx=1.0,dy=-1.0,spacing=1.0))
        image_file=STAGE/(name+'OrthoPixels.bytes');png(image_file,pixels[y:y+th,x:x+tw],valid_image[y:y+th,x:x+tw],lo,hi)
        image_meta=STAGE/(name+'Image.json');record=dict(key='Bundled'+name,source=IMAGE,kind='HiRISE',resource=PREFIX+name+'OrthoPixels',spacing=1.0,nativeResolution=False,
            originalSourceSpacingMeters=.25,displayResampling='Nearest 4:1 photographic decimation',west=bb[0],south=bb[1],east=bb[2],north=bb[3],
            rect=dict(x=bb[0]/360+.5,y=bb[1]/180+.5,z=(bb[2]-bb[0])/360,w=(bb[3]-bb[1])/180))
        write_json(image_meta,record);manifest['windows'].append(dict(metadata=PREFIX+name+'Height',heights=PREFIX+name+'Heights'));manifest['images'].append(PREFIX+name+'Image')
        runtime_files.extend([height_file,height_meta,image_file,image_meta]);reports.append(dict(index=index,nativeWindow=[x,y,tw,th],heightSha256=sha(height_file),imageSha256=sha(image_file)))
        print('Staged native tile',index+1,'/16',flush=True)

factor=max(WINDOW[2],WINDOW[3])/4096;ow=round(WINDOW[2]/factor);oh=round(WINDOW[3]/factor)
overview=gdal.Translate('',image,format='MEM',width=ow,height=oh,resampleAlg='average');a=overview.ReadAsArray()
overview_file=STAGE/'JezeroZoneOverviewPixels.bytes';png(overview_file,a,a!=image_nodata,lo,hi);runtime_files.append(overview_file)
bb=bounds(gt,WINDOW[2],WINDOW[3]);margin=10.0;mid=(bb[1]+bb[3])*.5;xmargin=margin/math.cos(math.radians(mid))
select=[math.degrees((gt[0]+xmargin)/R),math.degrees((gt[3]-WINDOW[3]+margin)/R),math.degrees((gt[0]+WINDOW[2]-xmargin)/R),math.degrees((gt[3]-margin)/R)]
definition=dict(name='Jezero / Western delta 10 km mission zone',west=select[0],south=select[1],east=select[2],north=select[3],spacingMeters=1.0,
    widthKm=(select[2]-select[0])*math.pi/180*R*math.cos(math.radians(mid))/1000,heightKm=(select[3]-select[1])*math.pi/180*R/1000,
    dtmSource=DEM,imageSource=IMAGE,imageResource=PREFIX+'JezeroZoneOverviewPixels',imageWest=bb[0],imageSouth=bb[1],imageEast=bb[2],imageNorth=bb[3],
    imageNativeSpacingMeters=.25,imageRuntimeSpacingMeters=1.0,imageOverviewSpacingMeters=factor,imageOverviewWidth=ow,imageOverviewHeight=oh,
    supportMarginMeters=10.0,validHeightFraction=1.0,validImageFraction=audit['validImageFraction'],elevationReferenceResource=PREFIX+'JezeroZoneElevationReference',
    dataNotice='Original USGS co-registered HiRISE TRN master 1 m height grid; matching .25 m source photograph decimated to a 1 m display grid. No elevation upsampling or gap filling. Original image nodata remains transparent. Source post spacing is not absolute measurement accuracy; some endpoint pairs have no feasible route.')
definition_file=STAGE/'JezeroZone.json';write_json(definition_file,definition);runtime_files.append(definition_file)
manifest_file=STAGE/'BundledWindows.json';write_json(manifest_file,manifest);runtime_files.append(manifest_file)
oracles=[]
for u,v in [(.12,.12),(.12,.88),(.5,.5),(.88,.12),(.88,.88)]:
    lat=float(np.float32(select[1]+(select[3]-select[1])*v));lon=float(np.float32(select[0]+(select[2]-select[0])*u));x=(math.radians(lon)*R-gt[0])-.5;y=(math.radians(lat)*R-gt[3])/(-1)-.5
    i=int(math.floor(x));j=int(math.floor(y));stencil=heights[j:j+2,i:i+2].astype(float);tx=x-i;ty=y-j
    h=float((stencil[0,0]*(1-tx)+stencil[0,1]*tx)*(1-ty)+(stencil[1,0]*(1-tx)+stencil[1,1]*tx)*ty)
    oracles.append(dict(latitude=lat,longitude=lon,heightMeters=h,nativeStencil=stencil.tolist(),nativePixelColumn=x,nativePixelRow=y))
oracle=dict(source=DEM,sourceCropSha256=audit['nativeHeightCropSha256'],description='Independent source-GeoTIFF bilinear heights at exact Unity Float32 latitude/longitude values; not application provider output',points=oracles)
oracle_file=STAGE/'JezeroZoneElevationReference.json';write_json(oracle_file,oracle);runtime_files.append(oracle_file)
audit.update(selectableBboxWestSouthEastNorth=select,selectableWidthKm=definition['widthKm'],selectableHeightKm=definition['heightKm'],tiles=reports,tileOverlapPixels=128,
    displayLow=float(lo),displayHigh=float(hi),runtimePayloadBytes=sum(p.stat().st_size for p in runtime_files),runtimeStageDirectory=str(STAGE),resourceManifest=PREFIX+'BundledWindows')
write_json(OUT/'coverage-audit.json',audit);write_json(OUT/'JezeroZone.json',definition);write_json(OUT/'runtime-resource-manifest.json',manifest);write_json(OUT/'independent-elevation-reference-points.json',oracle)
print('STAGE READY '+json.dumps(definition),flush=True);print('Runtime payload bytes',audit['runtimePayloadBytes'],flush=True)
