using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace ReplayRescue {
  public sealed class ReleaseAsset {
    public string name, browser_download_url, digest, state;
    public long size;
  }
  public sealed class ReleaseInfo {
    public string tag_name;
    public bool draft, prerelease;
    public ReleaseAsset[] assets;
    public Version Version { get { return Updater.ParseVersion(tag_name); } }
    public ReleaseAsset Package { get { return assets.Single(a=>a.name==Updater.AssetName); } }
  }
  public sealed class UpdatePlan {
    public string Root, Tag, Digest, Language;
    public long Size;
    public int ParentPid;
    public bool Tray;
  }
  public sealed class UpdateResult {
    public bool Success;
    public string Version, Error;
  }
  public static class Updater {
    internal const string AssetName="ReplayRescue-win-x64.zip";
    internal const string Repository="https://github.com/sjh7711/ReplayRescue";
    internal const long MaxPackage=32*1024*1024, MaxExpanded=128*1024*1024;
    public static Version CurrentVersion { get { var v=Assembly.GetExecutingAssembly().GetName().Version;return new Version(v.Major,v.Minor,v.Build); } }
    public static Version ParseVersion(string tag){
      if(tag==null || !Regex.IsMatch(tag,@"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$"))throw new InvalidDataException("업데이트 버전 형식이 올바르지 않습니다.");
      Version version;if(!Version.TryParse(tag.TrimStart('v'),out version))throw new InvalidDataException("업데이트 버전 형식이 올바르지 않습니다.");return version;
    }
    internal static ReleaseInfo ParseRelease(string json){
      var release=new JavaScriptSerializer().Deserialize<ReleaseInfo>(json);
      if(release==null || release.draft || release.prerelease || release.assets==null)throw new InvalidDataException("정식 릴리스 정보를 확인할 수 없습니다.");
      ParseVersion(release.tag_name);
      var assets=release.assets.Where(a=>a!=null && a.name==AssetName).ToArray();
      if(assets.Length!=1)throw new InvalidDataException("업데이트 ZIP을 찾을 수 없습니다.");
      var asset=assets[0];
      if(asset.state!="uploaded" || asset.size<=0 || asset.size>MaxPackage || !Regex.IsMatch(asset.digest ?? "",@"^sha256:[a-fA-F0-9]{64}$"))throw new InvalidDataException("업데이트 파일 검증 정보를 확인할 수 없습니다.");
      if(asset.browser_download_url!=Repository+"/releases/download/"+release.tag_name+"/"+AssetName)throw new InvalidDataException("업데이트 다운로드 주소가 올바르지 않습니다.");
      return release;
    }
    internal static bool AllowedDownload(Uri uri){return uri.Scheme=="https" && uri.Port==443 && uri.UserInfo=="" && (uri.Host=="github.com" || uri.Host=="release-assets.githubusercontent.com" || uri.Host=="objects.githubusercontent.com");}
    static HttpWebResponse Open(Uri address,bool metadata){
      ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
      for(int redirect=0;redirect<5;redirect++){
        if(metadata ? address.AbsoluteUri!="https://api.github.com/repos/sjh7711/ReplayRescue/releases/latest" : !AllowedDownload(address))throw new InvalidDataException("업데이트 다운로드 주소가 올바르지 않습니다.");
        var request=(HttpWebRequest)WebRequest.Create(address);
        request.UserAgent="ReplayRescue/"+CurrentVersion;request.Accept=metadata?"application/vnd.github+json":"application/octet-stream";
        request.Timeout=20000;request.ReadWriteTimeout=20000;request.AllowAutoRedirect=false;
        if(metadata)request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
        var response=(HttpWebResponse)request.GetResponse();
        if((int)response.StatusCode>=300 && (int)response.StatusCode<400){string next=response.Headers["Location"];response.Dispose();address=new Uri(address,next);continue;}
        if(response.StatusCode!=HttpStatusCode.OK){response.Dispose();throw new IOException("업데이트 서버 응답을 확인할 수 없습니다.");}
        return response;
      }
      throw new IOException("업데이트 다운로드 주소가 올바르지 않습니다.");
    }
    public static ReleaseInfo Check(){
      using(var response=Open(new Uri("https://api.github.com/repos/sjh7711/ReplayRescue/releases/latest"),true))
      using(var input=response.GetResponseStream())using(var output=new MemoryStream()){
        CopyLimited(input,output,1024*1024,null,0);return ParseRelease(Encoding.UTF8.GetString(output.ToArray()));
      }
    }
    static void CopyLimited(Stream input,Stream output,long limit,Action<int> progress,long expected){
      byte[] buffer=new byte[65536];long total=0;int count;
      while((count=input.Read(buffer,0,buffer.Length))>0){total+=count;if(total>limit)throw new InvalidDataException("업데이트 파일 크기가 올바르지 않습니다.");output.Write(buffer,0,count);if(progress!=null && expected>0)progress((int)Math.Min(100,total*100/expected));}
    }
    internal static void VerifyPackage(string path,string digest,long expectedSize){
      if(new FileInfo(path).Length!=expectedSize || expectedSize<=0 || expectedSize>MaxPackage)throw new InvalidDataException("업데이트 파일 크기가 올바르지 않습니다.");
      using(var sha=SHA256.Create())using(var input=File.OpenRead(path)){
        string actual="sha256:"+BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
        if(!string.Equals(actual,digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("업데이트 파일 검증에 실패했습니다.");
      }
    }
    public static string Prepare(ReleaseInfo release,bool tray,string language,Action<int> progress){
      if(release.Version<=CurrentVersion)throw new InvalidOperationException("이미 최신 버전입니다.");
      // Revalidate the trusted metadata before using any URL or filesystem input.
      release=ParseRelease(Files.Json(release));
      string stage=Path.Combine(Files.Data,"updates",Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(stage);EnsureNoLinks(stage);
      string archive=Path.Combine(stage,"package.zip");
      using(var response=Open(new Uri(release.Package.browser_download_url),false))using(var input=response.GetResponseStream())using(var output=File.Create(archive))CopyLimited(input,output,release.Package.size,progress,release.Package.size);
      VerifyPackage(archive,release.Package.digest,release.Package.size);
      ExtractPackage(archive,Path.Combine(stage,"payload"),release.Version);
      File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(stage,"ReplayRescue.Updater.exe"));
      string plan=Path.Combine(stage,"plan.json");
      Files.Write(plan,new UpdatePlan{Root=Files.Root,Tag=release.tag_name,Digest=release.Package.digest,Size=release.Package.size,ParentPid=Process.GetCurrentProcess().Id,Tray=tray,Language=language});
      return plan;
    }
    internal static string PackagePath(string entry){
      const string prefix="ReplayRescue/";
      if(!entry.StartsWith(prefix,StringComparison.Ordinal) || entry.Contains("\\"))throw new InvalidDataException("업데이트 ZIP 경로가 올바르지 않습니다.");
      string relative=entry.Substring(prefix.Length);var parts=relative.Split('/');
      if(parts.Any(p=>p.Length==0 || p=="." || p==".." || p.EndsWith(" ") || p.EndsWith(".") || p.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",RegexOptions.IgnoreCase)))throw new InvalidDataException("업데이트 ZIP 경로가 올바르지 않습니다.");
      string[] roots={"ReplayRescue.exe","extension-id.txt","README.md","README.ko.md","VALIDATION.md","GettingStarted.html","시작하기.html","app.manifest","build.ps1","install.ps1","uninstall.ps1",".gitignore",".gitattributes"};
      string[] directories={"extension","docs","src","tests","tools"};
      if(parts.Length==1?!roots.Contains(relative):!directories.Contains(parts[0]))throw new InvalidDataException("업데이트 ZIP에 허용되지 않은 파일이 있습니다.");
      return relative.Replace('/',Path.DirectorySeparatorChar);
    }
    internal static void EnsureNoLinks(string path){
      for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
        if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("업데이트 폴더에 링크된 경로를 사용할 수 없습니다.");
    }
    internal static string[] ExtractPackage(string archive,string payload,Version version){
      EnsureNoLinks(payload);Directory.CreateDirectory(payload);
      var files=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
      using(var zip=ZipFile.OpenRead(archive)){
        if(zip.Entries.Count>2000)throw new InvalidDataException("업데이트 ZIP에 파일이 너무 많습니다.");
        foreach(var entry in zip.Entries){
          if(entry.FullName.EndsWith("/"))continue;
          string relative=PackagePath(entry.FullName);
          if(!seen.Add(relative) || ((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("업데이트 ZIP 경로가 올바르지 않습니다.");
          total+=entry.Length;if(total>MaxExpanded)throw new InvalidDataException("업데이트 파일 크기가 올바르지 않습니다.");
          string target=Path.Combine(payload,relative);EnsureNoLinks(target);Directory.CreateDirectory(Path.GetDirectoryName(target));
          using(var input=entry.Open())using(var output=File.Create(target))CopyLimited(input,output,entry.Length,null,0);
          files.Add(relative);
        }
      }
      foreach(string required in new[]{"ReplayRescue.exe","extension-id.txt","extension"+Path.DirectorySeparatorChar+"manifest.json"})if(!seen.Contains(required))throw new InvalidDataException("업데이트 ZIP에 필수 파일이 없습니다.");
      var actual=AssemblyName.GetAssemblyName(Path.Combine(payload,"ReplayRescue.exe"));
      if(actual.Name!="ReplayRescue" || new Version(actual.Version.Major,actual.Version.Minor,actual.Version.Build)!=version)throw new InvalidDataException("업데이트 EXE 버전이 릴리스와 다릅니다.");
      if(File.ReadAllText(Path.Combine(payload,"extension-id.txt")).Trim()!="edlganjmnhkmjlnekkbpnhdpaiillfgc")throw new InvalidDataException("Chrome 확장 ID가 일치하지 않습니다.");
      return files.ToArray();
    }
    public static Process Launch(string plan){
      return Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(plan),"ReplayRescue.Updater.exe"),"--apply-update \""+plan+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
    }
    internal static bool IsInstalling(string root){
      string path=Path.Combine(root,"data","update.lock");if(!File.Exists(path))return false;
      try{using(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))return false;}catch(IOException){return true;}catch(UnauthorizedAccessException){return true;}
    }
    internal static void ReplaceFiles(string payload,string root,string backup,string[] files,Action<int> afterCopy=null){
      var changed=new List<string>();var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try{
        foreach(string relative in files){
          string target=Path.Combine(root,relative),saved=Path.Combine(backup,relative);EnsureNoLinks(target);EnsureNoLinks(saved);
          Directory.CreateDirectory(Path.GetDirectoryName(target));Directory.CreateDirectory(Path.GetDirectoryName(saved));
          if(File.Exists(target)){File.Copy(target,saved,false);existing.Add(relative);}
          InstallFile(Path.Combine(payload,relative),target);changed.Add(relative);
          if(afterCopy!=null)afterCopy(changed.Count);
        }
      }catch{
        var errors=new List<Exception>();
        foreach(string relative in changed.AsEnumerable().Reverse())try{string target=Path.Combine(root,relative);if(existing.Contains(relative))InstallFile(Path.Combine(backup,relative),target);else if(File.Exists(target))File.Delete(target);}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("이전 파일 복구에 실패했습니다. 업데이트 백업을 확인하세요.",errors);
        throw;
      }
    }
    static void InstallFile(string source,string target){
      string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
      try{File.Copy(source,temporary,false);if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);}
      finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static int Apply(string planPath){
      UpdatePlan plan=null;string root=null;bool success=false,restart=false;
      try{
        string stage=Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if(Path.GetFullPath(planPath)!=Path.Combine(stage,"plan.json"))throw new InvalidDataException("Invalid update plan");
        plan=Files.Read<UpdatePlan>(planPath);string candidateRoot=Path.GetFullPath(plan.Root).TrimEnd(Path.DirectorySeparatorChar);
        string stageName=Path.GetFileName(stage);
        if(!Regex.IsMatch(stageName,"^[a-f0-9]{32}$") || !string.Equals(stage,Path.Combine(candidateRoot,"data","updates",stageName),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid update location");
        EnsureNoLinks(stage);EnsureNoLinks(candidateRoot);root=candidateRoot;
        var version=ParseVersion(plan.Tag);if(version<=CurrentVersion)throw new InvalidDataException("Invalid target version");
        string package=Path.Combine(stage,"package.zip");VerifyPackage(package,plan.Digest,plan.Size);
        string payload=Path.Combine(stage,"verified");var files=ExtractPackage(package,payload,version);
        string targetExe=Path.Combine(root,"ReplayRescue.exe");
        using(var gate=File.Open(Path.Combine(root,"data","update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
          Process parent=null;
          try{
            try{parent=Process.GetProcessById(plan.ParentPid);}catch(ArgumentException){}
            if(parent!=null && !string.Equals(parent.MainModule.FileName,targetExe,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid parent process");
            Files.Write(Path.Combine(stage,"ready.json"),new{ready=true});
            if(parent!=null && !parent.WaitForExit(30000))throw new IOException("앱 종료를 기다리는 시간이 초과되었습니다.");
          }finally{if(parent!=null)parent.Dispose();}
          restart=true;
          foreach(var process in Process.GetProcessesByName("ReplayRescue"))using(process){try{if(!string.Equals(process.MainModule.FileName,targetExe,StringComparison.OrdinalIgnoreCase))continue;process.Kill();if(!process.WaitForExit(5000))throw new IOException("Chrome 연결 프로세스를 종료하지 못했습니다.");}catch(InvalidOperationException){}}
          ReplaceFiles(payload,root,Path.Combine(stage,"backup"),files);
          success=true;
        }
      }catch(Exception error){
        if(root!=null && plan!=null)try{Files.Write(Path.Combine(root,"data","update-result.json"),new UpdateResult{Success=false,Version=plan.Tag,Error=error.Message});}catch{}
      }
      if(root==null || plan==null || !restart)return 1;
      try{
        if(success)Files.Write(Path.Combine(root,"data","update-result.json"),new UpdateResult{Success=true,Version=plan.Tag});
        Process.Start(new ProcessStartInfo(Path.Combine(root,"ReplayRescue.exe"),plan.Tray?"--tray":""){UseShellExecute=false,CreateNoWindow=plan.Tray,WindowStyle=plan.Tray?ProcessWindowStyle.Hidden:ProcessWindowStyle.Normal});
      }catch{return 1;}
      return success?0:1;
    }
  }
}
