using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace MarsPath.Editor
{
    internal sealed class MarsQuestRadioManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XDocument manifest = XDocument.Load(file);
            foreach (string permission in new[] { "android.permission.CHANGE_WIFI_MULTICAST_STATE", "android.permission.RECORD_AUDIO" })
            {
                if (!manifest.Root.Elements("uses-permission").Any(node => (string)node.Attribute(android + "name") == permission))
                    manifest.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission)));
            }
            manifest.Save(file);
        }
    }
}
