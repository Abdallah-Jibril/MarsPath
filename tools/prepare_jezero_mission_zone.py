"""Bounded, native-pixel Jezero mission-zone preparation. Run with QGIS Python.

Only height subwindows are copied; no elevations are resampled or gap-filled.
The image overview is a separately labelled display reduction of RED_C.
"""
import os,json,math,hashlib,datetime,uuid
from pathlib import Path
import numpy as np
from osgeo import gdal

gdal.UseExceptions()
os.environ['GDAL_HTTP_TIMEOUT']='55'
os.environ['GDAL_HTTP_CONNECTTIMEOUT']='15'
os.environ['GDAL_DISABLE_READDIR_ON_OPEN']='EMPTY_DIR'
os.environ['CPL_VSIL_CURL_ALLOWED_EXTENSIONS']='.tif'
os.environ['CPL_VSIL_CURL_CACHE_SIZE']='16777216'
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'GIS/OfficialSurfaceSources/JezeroMissionZone'
RES=ROOT/'MarsPathUnity/Assets/Resources/NASA/MissionZone'
OUT.mkdir(parents=True,exist_ok=True)
RES.mkdir(parents=True,exist_ok=True)
BASE='https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/'
DEM=BASE+'DTEEC_045994_1985_046060_1985_U01.tif'
IMAGE=BASE+'ESP_045994_1985_RED_C_01_ORTHO.tif'
R=3396190.0
PREFIX='NASA/MissionZone/'

def write_json(path,value):path.write_text(json.dumps(value,indent=2),encoding='utf-8')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def meta(ds,url):
    b=ds.GetRasterBand(1)
    return dict(source=url,width=ds.RasterXSize,height=ds.RasterYSize,transform=list(ds.GetGeoTransform()),projection=ds.GetProjection(),type=gdal.GetDataTypeName(b.DataType),nodata=b.GetNoDataValue(),scale=b.GetScale(),offset=b.GetOffset())
def valid(a,nodata):return np.isfinite(a)&(a!=nodata) if nodata is not None else np.isfinite(a)
def bounds(gt,w,h):return [math.degrees(gt[0]/R),math.degrees((gt[3]+h*gt[5])/R),math.degrees((gt[0]+w*gt[1])/R),math.degrees(gt[3]/R)]
def window_for_bounds(gt,bbox):
    west,south,east,north=[math.radians(v)*R for v in bbox]
    x0=math.floor((west-gt[0])/gt[1]);x1=math.ceil((east-gt[0])/gt[1])
    y0=math.floor((north-gt[3])/gt[5]);y1=math.ceil((south-gt[3])/gt[5])
    return [x0,y0,x1-x0,y1-y0]
def source_progress(label):
    last=[-1]
    def cb(p,message,data):
        n=int(p*10)
        if n>last[0]:print(label+': '+str(n*10)+'%',flush=True);last[0]=n
        return 1
    return cb
def local_tiff(destination,src,window,label):
    if destination.exists():
        cached=gdal.Open(str(destination))
        if cached.RasterXSize==window[2] and cached.RasterYSize==window[3]:return cached
        cached=None
    return gdal.Translate(str(destination),src,format='GTiff',srcWin=window,creationOptions=['TILED=YES','COMPRESS=DEFLATE','NUM_THREADS=2'],callback=source_progress(label))
def png(path,a,mask,low,high):
    h,w=a.shape
    d=gdal.GetDriverByName('MEM').Create('',w,h,4,gdal.GDT_Byte)
    stretched=np.clip((a.astype(np.float32)-low)*(255/max(high-low,1e-6)),0,255).astype(np.uint8)
    for b in range(1,4):d.GetRasterBand(b).WriteArray(stretched)
    d.GetRasterBand(4).WriteArray(mask.astype(np.uint8)*255)
    gdal.GetDriverByName('PNG').CreateCopy(str(path),d)
def unity_meta(path):
    p=Path(str(path)+'.meta')
    if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')

print('Reading original HiRISE native metadata',flush=True)
source=gdal.Open('/vsicurl/'+DEM)
source_meta=meta(source,DEM)
write_json(OUT/'original-dtm-source-metadata.json',source_meta)
print(json.dumps(source_meta),flush=True)
gt=source.GetGeoTransform()
# This interior includes the already independently checked scarp near
# 18.462055 N,77.424727 E and avoids the rotated source's left/right borders.
requested=[1024,2048,4224,6000]
dtm_path=OUT/'JezeroMissionZoneNativeDtm.tif'
dtm=local_tiff(dtm_path,source,requested,'Native height crop')
heights=dtm.ReadAsArray();height_valid=valid(heights,source_meta['nodata'])&(heights>-10000)&(heights<30000)
height_gt=dtm.GetGeoTransform();bbox=bounds(height_gt,dtm.RasterXSize,dtm.RasterYSize)
audit=dict(dtmSource=source_meta,cropSourceWindow=requested,cropTransform=list(height_gt),heightBboxWestSouthEastNorth=bbox,
    heightWidth=dtm.RasterXSize,heightHeight=dtm.RasterYSize,validHeightCount=int(height_valid.sum()),heightSampleCount=int(height_valid.size),
    invalidHeightCount=int(height_valid.size-height_valid.sum()),heightMinimumMeters=float(heights[height_valid].min()),heightMaximumMeters=float(heights[height_valid].max()),
    heightFileSha256=sha(dtm_path),heightRawValuesSha256=hashlib.sha256(heights.tobytes(order='C')).hexdigest())
write_json(OUT/'coverage-audit.json',audit)
print('HEIGHT AUDIT '+json.dumps({k:v for k,v in audit.items() if k!='dtmSource'}),flush=True)
if not height_valid.all():
    # A partially valid rectangle is never silently declared a supported zone.
    np.save(OUT/'height-valid-mask.npy',height_valid)
    raise RuntimeError('Requested native height rectangle contains nodata. Inspect valid-mask audit before choosing another rectangle.')

print('Reading matching native RED_C photograph',flush=True)
image_source=gdal.Open('/vsicurl/'+IMAGE);image_meta=meta(image_source,IMAGE);image_window=window_for_bounds(image_source.GetGeoTransform(),bbox)
write_json(OUT/'original-image-source-metadata.json',image_meta)
image_path=OUT/'JezeroMissionZoneNativeRedC.tif'
image=local_tiff(image_path,image_source,image_window,'Native matching image crop')
pixels=image.ReadAsArray();image_valid=valid(pixels,image_meta['nodata']);image_gt=image.GetGeoTransform();image_bbox=bounds(image_gt,image.RasterXSize,image.RasterYSize)
lo,hi=np.percentile(pixels[image_valid],[.5,99.5])
mask_ds=gdal.GetDriverByName('GTiff').Create(str(OUT/'JezeroMissionZoneImageValidity.tif'),image.RasterXSize,image.RasterYSize,1,gdal.GDT_Byte,options=['COMPRESS=DEFLATE','TILED=YES'])
mask_ds.SetGeoTransform(image_gt);mask_ds.SetProjection(image.GetProjection());mask_ds.GetRasterBand(1).WriteArray(image_valid.astype(np.uint8));mask_ds=None
audit.update(imageSource=image_meta,imageCropSourceWindow=image_window,imageCropTransform=list(image_gt),imageBboxWestSouthEastNorth=image_bbox,
    validImageCount=int(image_valid.sum()),imageSampleCount=int(image_valid.size),invalidImageCount=int(image_valid.size-image_valid.sum()),
    validImageFraction=float(image_valid.mean()),imageFileSha256=sha(image_path),imageRawValuesSha256=hashlib.sha256(pixels.tobytes(order='C')).hexdigest(),displayLow=float(lo),displayHigh=float(hi),
    displayScaling='One shared linear 0.5%-99.5% source-DN stretch for all native image tiles and overview; source nodata is alpha zero. No fabricated or filled image pixels.')

# Four native crops with 128-source-pixel overlap in both directions.
manifest=dict(retrieved=datetime.datetime.now(datetime.timezone.utc).isoformat(),source='Official USGS M2020 Jezero HiRISE native mission-zone crops',windows=[],images=[])
tile_width=(heights.shape[1]+128)//2;tile_height=(heights.shape[0]+128)//2
runtime_files=[];tile_reports=[]
for row in range(2):
    for col in range(2):
        index=row*2+col;x0=0 if col==0 else heights.shape[1]-tile_width;y0=0 if row==0 else heights.shape[0]-tile_height
        tile=heights[y0:y0+tile_height,x0:x0+tile_width]
        tile_gt=[height_gt[0]+x0*height_gt[1],height_gt[1],0,height_gt[3]+y0*height_gt[5],0,height_gt[5]]
        label='JezeroZoneTile'+str(index)
        tile_meta=dict(id='DTEEC_045994_1985_046060_1985_U01_MISSION_ZONE_'+str(index),kind='HiRISE',source=DEM,
            width=tile_width,height=tile_height,west=tile_gt[0],north=tile_gt[3],dx=tile_gt[1],dy=tile_gt[5],spacing=abs(tile_gt[1]))
        height_file=RES/(label+'Heights.bytes');height_file.write_bytes(tile.astype('<f4',copy=False).tobytes(order='C'))
        metadata_file=RES/(label+'Height.json');write_json(metadata_file,tile_meta)
        manifest['windows'].append(dict(metadata=PREFIX+label+'Height',heights=PREFIX+label+'Heights'))
        tile_bbox=bounds(tile_gt,tile_width,tile_height)
        iw=window_for_bounds(image_gt,tile_bbox);ix,iy,ww,hh=iw
        image_tile=pixels[iy:iy+hh,ix:ix+ww];valid_tile=image_valid[iy:iy+hh,ix:ix+ww]
        if image_tile.shape!=(hh,ww):raise RuntimeError('Image tile bounds exceeded preserved source crop')
        image_tile_gt=[image_gt[0]+ix*image_gt[1],image_gt[1],0,image_gt[3]+iy*image_gt[5],0,image_gt[5]]
        ib=bounds(image_tile_gt,ww,hh)
        image_file=RES/(label+'OrthoPixels.bytes');png(image_file,image_tile,valid_tile,lo,hi)
        record=dict(key='Bundled'+label+'RedC',source=IMAGE,kind='HiRISE',resource=PREFIX+label+'OrthoPixels',nativeResolution=True,
            spacing=abs(image_gt[1]),west=ib[0],south=ib[1],east=ib[2],north=ib[3],rect=dict(x=ib[0]/360+.5,y=ib[1]/180+.5,z=(ib[2]-ib[0])/360,w=(ib[3]-ib[1])/180))
        image_metadata=RES/(label+'Image.json');write_json(image_metadata,record);manifest['images'].append(PREFIX+label+'Image')
        runtime_files.extend([height_file,metadata_file,image_file,image_metadata])
        tile_reports.append(dict(index=index,localNativeWindow=[x0,y0,tile_width,tile_height],heightResource=PREFIX+label+'Heights',heightSha256=sha(height_file),
            imageNativeWindowWithinMainCrop=iw,imageResource=record['resource'],imageSha256=sha(image_file)))

overview_factor=max(image.RasterXSize,image.RasterYSize)/4096
overview_width=int(round(image.RasterXSize/overview_factor));overview_height=int(round(image.RasterYSize/overview_factor))
overview=gdal.Translate('',image,format='MEM',width=overview_width,height=overview_height,resampleAlg='average')
overview_pixels=overview.ReadAsArray();overview_valid=valid(overview_pixels,image_meta['nodata'])
overview_file=RES/'JezeroZoneOverviewPixels.bytes';png(overview_file,overview_pixels,overview_valid,lo,hi);runtime_files.append(overview_file)
margin=10.0
select_bbox=[math.degrees((height_gt[0]+margin)/R),math.degrees((height_gt[3]+heights.shape[0]*height_gt[5]+margin)/R),
    math.degrees((height_gt[0]+heights.shape[1]*height_gt[1]-margin)/R),math.degrees((height_gt[3]-margin)/R)]
lat_middle=(select_bbox[1]+select_bbox[3])*.5
definition=dict(name='Jezero / Western delta mission zone',west=select_bbox[0],south=select_bbox[1],east=select_bbox[2],north=select_bbox[3],
    spacingMeters=abs(height_gt[1]),widthKm=(select_bbox[2]-select_bbox[0])*math.pi/180*R*math.cos(math.radians(lat_middle))/1000,
    heightKm=(select_bbox[3]-select_bbox[1])*math.pi/180*R/1000,dtmSource=DEM,imageSource=IMAGE,imageResource=PREFIX+'JezeroZoneOverviewPixels',
    imageWest=image_bbox[0],imageSouth=image_bbox[1],imageEast=image_bbox[2],imageNorth=image_bbox[3],
    imageNativeSpacingMeters=abs(image_gt[1]),imageOverviewSpacingMeters=abs(image_gt[1])*overview_factor,
    imageOverviewWidth=overview_width,imageOverviewHeight=overview_height,supportMarginMeters=margin,
    validHeightFraction=1.0,validImageFraction=float(image_valid.mean()),
    dataNotice='Original measured HiRISE DTM with matching RED_C imagery. Image source nodata is retained transparently, not filled. Source spacing is not absolute accuracy. Endpoints inside this zone may still have no feasible route under the selected constraints.')
definition_path=RES/'JezeroZone.json';write_json(definition_path,definition);runtime_files.append(definition_path)
manifest_path=RES/'BundledWindows.json';write_json(manifest_path,manifest);runtime_files.append(manifest_path)
for file in runtime_files:unity_meta(file)
audit.update(selectableBboxWestSouthEastNorth=select_bbox,selectablePhysicalWidthKm=definition['widthKm'],selectablePhysicalHeightKm=definition['heightKm'],supportMarginMeters=margin,
    tiles=tile_reports,tileOverlapPixels=128,tileOverlapMeters=128*abs(height_gt[1]),runtimePayloadBytes=sum(p.stat().st_size for p in runtime_files),
    resourceManifest=PREFIX+'BundledWindows',definitionResource=PREFIX+'JezeroZone',overviewDisplaySpacingMeters=definition['imageOverviewSpacingMeters'])
write_json(OUT/'coverage-audit.json',audit);write_json(OUT/'JezeroZone.json',definition);write_json(OUT/'runtime-resource-manifest.json',manifest)
print('ZONE READY '+json.dumps(definition),flush=True)
print('AUDIT '+json.dumps(dict(validHeightFraction=1,validImageFraction=definition['validImageFraction'],runtimePayloadBytes=audit['runtimePayloadBytes'],heightRangeMeters=[audit['heightMinimumMeters'],audit['heightMaximumMeters']],tiles=tile_reports)),flush=True)
