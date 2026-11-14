from pathlib import Path
from osgeo import gdal
import numpy as np,json,math
root=Path(__file__).resolve().parents[1]
folder=root/'GIS/OfficialSurfaceSources/JezeroMissionZone'
d=gdal.Open(str(folder/'JezeroMissionZoneNativeDtm.tif'));a=d.ReadAsArray();g=d.GetGeoTransform()
z=json.loads((root/'MarsPathUnity/Assets/Resources/NASA/MissionZone/JezeroZone.json').read_text());pts=[];r=3396190
for u,v in [(.18,.18),(.18,.82),(.5,.5),(.82,.18),(.82,.82)]:
    lat=float(np.float32(z['south']+(z['north']-z['south'])*v));lon=float(np.float32(z['west']+(z['east']-z['west'])*u))
    x=(math.radians(lon)*r-g[0])/g[1]-.5;y=(math.radians(lat)*r-g[3])/g[5]-.5
    i=int(math.floor(x));j=int(math.floor(y));tx=x-i;ty=y-j;b=a[j:j+2,i:i+2].astype(float)
    h=(b[0,0]*(1-tx)+b[0,1]*tx)*(1-ty)+(b[1,0]*(1-tx)+b[1,1]*tx)*ty
    pts.append(dict(latitude=lat,longitude=lon,heightMeters=float(h),sourcePixelColumn=x,sourcePixelRow=y,nativeStencil=b.tolist()))
report=dict(source=z['dtmSource'],sourceHash=json.loads((folder/'coverage-audit.json').read_text())['heightFileSha256'],description='Independent GDAL original native crop bilinear samples using actual Unity float coordinate values; source-metadata guarded',points=pts)
(folder/'independent-elevation-reference-points.json').write_text(json.dumps(report,indent=2))
(root/'MarsPathUnity/Assets/Resources/NASA/MissionZone/Jezero4kmElevationReference.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
