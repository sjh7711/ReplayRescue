using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;

namespace ReplayRescue {
  public static class UpdateTests {
    static string Digest(string path){using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return "sha256:"+BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
    static bool Rejects(Action action){try{action();return false;}catch{return true;}}
    internal static void Run(Action<bool,string> assert){
      assert(Updater.ParseVersion("v1.3.10")>Updater.ParseVersion("v1.3.9"),"Update versions compare numerically");
      assert(Rejects(()=>Updater.ParseVersion("v1.3.1-beta")),"Prerelease version syntax is rejected");
      var metadata=new ReleaseInfo{tag_name="v9.0.0",assets=new[]{new ReleaseAsset{name=Updater.AssetName,state="uploaded",size=123,digest="sha256:"+new string('a',64),browser_download_url=Updater.Repository+"/releases/download/v9.0.0/"+Updater.AssetName}}};
      assert(Updater.ParseRelease(Files.Json(metadata)).Version==new Version(9,0,0),"Release metadata and SHA256 information round-trip");
      metadata.assets[0].browser_download_url="https://example.com/update.zip";
      assert(Rejects(()=>Updater.ParseRelease(Files.Json(metadata))),"Updates reject assets outside the configured repository");
      metadata.assets[0].browser_download_url=Updater.Repository+"/releases/download/v9.0.0/"+Updater.AssetName;
      metadata.assets[0].digest=null;assert(Rejects(()=>Updater.ParseRelease(Files.Json(metadata))),"Updates require a SHA256 digest");
      metadata.assets[0].digest="sha256:"+new string('a',64);metadata.prerelease=true;
      assert(Rejects(()=>Updater.ParseRelease(Files.Json(metadata))),"Automatic updates reject prereleases");
      assert(!Updater.AllowedDownload(new Uri("http://github.com/file")) && !Updater.AllowedDownload(new Uri("https://github.com.evil.example/file")),"Update redirects require HTTPS and an allowed host");
      foreach(string path in new[]{"ReplayRescue/../outside.exe","ReplayRescue/data/settings.json","ReplayRescue/.git/config","ReplayRescue/src/../../outside.exe","ReplayRescue/src/evil.cs:stream","ReplayRescue/src/CON.cs","ReplayRescue/src/file. ","../ReplayRescue.exe","ReplayRescue\\ReplayRescue.exe"})assert(Rejects(()=>Updater.PackagePath(path)),"ZIP rejects unsafe or preserved path: "+path);
      var migrated=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Settings>("{\"PollSeconds\":15}");
      assert(migrated.CheckUpdates && migrated.PollSeconds==15,"Existing preferences gain update checks without changing the interval");
      string temp=Path.Combine(Path.GetTempPath(),"ReplayRescue-update-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
      try{
        string zipPath=Path.Combine(temp,"valid.zip");
        using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){
          zip.CreateEntryFromFile(Assembly.GetExecutingAssembly().Location,"ReplayRescue/ReplayRescue.exe");
          using(var text=new StreamWriter(zip.CreateEntry("ReplayRescue/extension-id.txt").Open()))text.Write("edlganjmnhkmjlnekkbpnhdpaiillfgc");
          using(var text=new StreamWriter(zip.CreateEntry("ReplayRescue/extension/manifest.json").Open()))text.Write("{}");
        }
        string digest=Digest(zipPath);long size=new FileInfo(zipPath).Length;
        Updater.VerifyPackage(zipPath,digest,size);
        assert(Rejects(()=>Updater.VerifyPackage(zipPath,"sha256:"+new string('0',64),size)),"Tampered package digest is rejected");
        assert(Rejects(()=>Updater.VerifyPackage(zipPath,digest,size+1)),"Truncated or oversized package is rejected");
        var files=Updater.ExtractPackage(zipPath,Path.Combine(temp,"unpacked"),Updater.CurrentVersion);
        assert(files.Contains("ReplayRescue.exe"),"A valid package extracts and validates its executable version");
        assert(Rejects(()=>Updater.ExtractPackage(zipPath,Path.Combine(temp,"wrong-version"),new Version(9,9,9))),"Mismatched executable version is rejected");
        using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Update))zip.CreateEntry("ReplayRescue/../escaped.txt");
        assert(Rejects(()=>Updater.ExtractPackage(zipPath,Path.Combine(temp,"bad-zip"),Updater.CurrentVersion)) && !File.Exists(Path.Combine(temp,"escaped.txt")),"ZIP traversal cannot write outside staging");
        string root=Path.Combine(temp,"target"),payload=Path.Combine(temp,"payload");Directory.CreateDirectory(root);Directory.CreateDirectory(payload);Directory.CreateDirectory(Path.Combine(root,"data"));
        File.WriteAllText(Path.Combine(root,"README.md"),"old");File.WriteAllText(Path.Combine(root,"data","settings.json"),"keep");File.WriteAllText(Path.Combine(payload,"README.md"),"new");File.WriteAllText(Path.Combine(payload,"README.ko.md"),"new Korean");
        assert(Rejects(()=>Updater.ReplaceFiles(payload,root,Path.Combine(temp,"backup-failed"),new[]{"README.md","README.ko.md"},count=>{if(count==2)throw new IOException("Injected failure");})),"Installer reports an interrupted file replacement");
        assert(File.ReadAllText(Path.Combine(root,"README.md"))=="old" && !File.Exists(Path.Combine(root,"README.ko.md")),"Failed install restores previous files and removes newly added files");
        Updater.ReplaceFiles(payload,root,Path.Combine(temp,"backup-ok"),new[]{"README.md","README.ko.md"});
        assert(File.ReadAllText(Path.Combine(root,"README.md"))=="new" && File.ReadAllText(Path.Combine(root,"data","settings.json"))=="keep","Successful install replaces app files while preserving settings");
        using(var gate=File.Open(Path.Combine(root,"data","update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))assert(Updater.IsInstalling(root),"Installer lock blocks competing app and Chrome host launches");
        assert(!Updater.IsInstalling(root),"A released installer lock never leaves the app blocked");
      }finally{Directory.Delete(temp,true);}
    }
  }
}
