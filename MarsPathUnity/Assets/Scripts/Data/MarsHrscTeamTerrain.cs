using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MarsPath
{
    public static partial class MarsHrscStereoTerrain
    {
        [Serializable]public sealed class TeamAsset{public string id,url,release;public double[] bbox,polygon;public float imageGridMeters;}
        [Serializable]private sealed class TeamIndex{public TeamAsset[] assets;}
        private static TeamAsset[] teamAssets;
        private static readonly Dictionary<int,List<TeamAsset>> teamGrid=new Dictionary<int,List<TeamAsset>>();
        private static readonly List<TeamAsset> teamWide=new List<TeamAsset>();
        private static readonly Dictionary<string,float> teamRetry=new Dictionary<string,float>();
        private static readonly List<Strip> teamDiskIndex=new List<Strip>();
        private static readonly HashSet<long> teamRestoredCells=new HashSet<long>();
        private static bool teamDiskLoaded;
        private static string TeamCache=>Path.Combine(Application.persistentDataPath,"HrscTeam","v1");
        public static int TeamCatalogCount{get{LoadTeamIndex();return teamAssets.Length;}}
        private static void LoadTeamIndex()
        {
            if(teamAssets!=null)return;TextAsset file=Resources.Load<TextAsset>("NASA/HRSC/TeamCatalog");teamAssets=file==null?new TeamAsset[0]:JsonUtility.FromJson<TeamIndex>(file.text).assets;
            foreach(TeamAsset a in teamAssets)
            {
                if(a.bbox==null||a.bbox.Length!=4)continue;
                int x0=(int)Math.Floor(a.bbox[0]),x1=(int)Math.Floor(a.bbox[2]),y0=(int)Math.Floor(a.bbox[1]),y1=(int)Math.Floor(a.bbox[3]);
                if((long)(x1-x0+1)*(y1-y0+1)>256||x0< -180||x1>180){teamWide.Add(a);continue;}
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){int key=(y+90)*361+x+180;if(!teamGrid.TryGetValue(key,out List<TeamAsset> list)){list=new List<TeamAsset>();teamGrid.Add(key,list);}list.Add(a);}
            }
        }
        public static TeamAsset[] GlobalCandidates(GeoPoint focus)
        {
            LoadTeamIndex();int key=((int)Math.Floor(focus.latitude)+90)*361+(int)Math.Floor(focus.longitude)+180;
            IEnumerable<TeamAsset> candidates=teamGrid.TryGetValue(key,out List<TeamAsset> local)?local:Enumerable.Empty<TeamAsset>();
            return candidates.Concat(teamWide).Where(a=>TeamContains(a,focus)).OrderBy(a=>a.imageGridMeters).Take(4).ToArray();
        }
        private static bool TeamContains(TeamAsset a,GeoPoint p)
        {
            if(a.polygon==null||a.polygon.Length<6)return false;double x=p.longitude,y=p.latitude,around=(a.bbox[0]+a.bbox[2])*.5;
            while(x-around>180)x-=360;while(x-around< -180)x+=360;
            if(x<a.bbox[0]||x>a.bbox[2]||y<a.bbox[1]||y>a.bbox[3])return false;
            bool inside=false;int count=a.polygon.Length/2;
            for(int i=0,j=count-1;i<count;j=i++)
            {double xi=a.polygon[i*2],yi=a.polygon[i*2+1],xj=a.polygon[j*2],yj=a.polygon[j*2+1];while(xi-x>180)xi-=360;while(xi-x< -180)xi+=360;while(xj-x>180)xj-=360;while(xj-x< -180)xj+=360;if((yi>y)!=(yj>y)&&x<(xj-xi)*(y-yi)/(yj-yi)+xi)inside=!inside;}
            return inside;
        }
        private static bool TeamWindowContains(Strip s,GeoPoint p)
        {
            double latitude=p.latitude*Math.PI/180,dl=MarsGeospatial.NormalizeLongitude(p.longitude-(float)s.longitude)*Math.PI/180;
            double x=(s.radius*Math.Cos(latitude)*dl-s.west)/s.spacing-.5,y=(s.north-s.radius*latitude)/s.spacing-.5;
            return x>2&&y>2&&x<s.width-3&&y<s.height-3;
        }
        private static string TeamKey(TeamAsset a,GeoPoint p)=>a.id+"_"+Math.Floor(p.latitude/.05)+"_"+Math.Floor(p.longitude/.05);
        private static void RestoreTeamCachedGeometry(GeoPoint p)
        {
            if(!MarsTerrainCachePolicy.ReadPersistentCache)return;
            long cell=((long)Math.Floor((p.latitude+90)*20)<<32)|(uint)Math.Floor((p.longitude+180)*20);
            if(!teamRestoredCells.Add(cell))return;
            if(!teamDiskLoaded)
            {
                teamDiskLoaded=true;teamDiskIndex.Clear();if(Directory.Exists(TeamCache))foreach(string file in Directory.GetFiles(TeamCache,"*.json"))try{Strip s=JsonUtility.FromJson<Strip>(File.ReadAllText(file));s.resource=Path.GetFileNameWithoutExtension(file);teamDiskIndex.Add(s);}catch(Exception){}
            }
            foreach(Strip info in teamDiskIndex)if(TeamWindowContains(info,p)&&!strips.Any(s=>s.id==info.id&&TeamWindowContains(s,p)))try
            {
                Strip s=JsonUtility.FromJson<Strip>(JsonUtility.ToJson(info));s.bytes=File.ReadAllBytes(Path.Combine(TeamCache,info.resource+".bytes"));s.resource=null;
                if(ValidLayout(s)&&TeamHash(s.bytes)==s.sha256){strips.Add(s);strips.Sort((a,b)=>b.spacing.CompareTo(a.spacing));Revision++;}
            }catch(Exception){}
        }
        private static string TeamHash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        public static IEnumerator PrepareNativeWindowIfAvailable(GeoPoint focus,Func<bool> cancelled=null)=>Prepare(focus,cancelled);
        private static IEnumerator PrepareTeam(GeoPoint focus,Func<bool> cancelled)
        {
            RestoreTeamCachedGeometry(focus);
            float existingSpacing=Spacing(focus);
            if(existingSpacing>0&&existingSpacing<=75)yield break;
            int count=0;foreach(TeamAsset asset in GlobalCandidates(focus))
            {
                if(cancelled!=null&&cancelled()||count++>=3)yield break;
                string key=TeamKey(asset,focus);if(teamRetry.TryGetValue(key,out float retry)&&Time.realtimeSinceStartup<retry)continue;
                teamRetry[key]=Time.realtimeSinceStartup+45;MarsCogWindow.Window window=null;
                yield return MarsCogWindow.Read(asset.url,focus,3000,cancelled,null,w=>window=w,true);
                if(window==null){LastError=MarsCogWindow.LastError;continue;}
                if(!window.sinusoidal||window.spacing>200||existingSpacing>0&&window.spacing>=existingSpacing)continue;
                var s=new Strip{id=asset.id,url=asset.url,sourceLabel="ESA / MEX HRSC science-team / preliminary",width=window.width,height=window.height,dataOffset=16,radius=window.radius,longitude=window.longitude,west=window.west,north=window.north,spacing=window.spacing,bytes=new byte[16+window.values.Length*2]};
                for(int i=0;i<window.values.Length;i++){float h=window.values[i];short v=float.IsNaN(h)||h<=-32768||h>30000?short.MinValue:(short)h;int k=16+i*2;s.bytes[k]=(byte)(v>>8);s.bytes[k+1]=(byte)v;}
                if(!Sample(s,focus,out _,out _))continue;s.sha256=TeamHash(s.bytes);
                try{Directory.CreateDirectory(TeamCache);File.WriteAllBytes(Path.Combine(TeamCache,key+".bytes"),s.bytes);File.WriteAllText(Path.Combine(TeamCache,key+".json"),JsonUtility.ToJson(s));}catch(IOException){}
                strips.Add(s);strips.Sort((a,b)=>b.spacing.CompareTo(a.spacing));Revision++;teamDiskLoaded=false;teamRestoredCells.Clear();yield break;
            }
        }
    }
}
