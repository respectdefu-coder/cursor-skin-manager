using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class ImportedTheme {public int Id;public string Name;public string[] Files;}
public static class Library {
    public static readonly Dictionary<Skin,ImportedTheme> Imported=new Dictionary<Skin,ImportedTheme>();
    public static readonly List<string> Warnings=new List<string>();
    public static readonly string[] RoleNames={"普通选择","帮助选择","后台运行","忙","精确选择","文本选择","手写","不可用","垂直调整","水平调整","斜向调整 1","斜向调整 2","移动","候选选择","链接选择","位置选择","人员选择"};
    public static string ImportRoot {get{return Path.Combine(Engine.Root,"imports");}}
    public static void Load() {
        Imported.Clear();Warnings.Clear();Engine.CustomSkins.Clear();
        if(!Directory.Exists(ImportRoot))return;
        foreach(string dir in Directory.GetDirectories(ImportRoot)) {
            if(Path.GetFileName(dir).StartsWith("_"))continue;
            try {
                var theme=XmlStore.Load<ImportedTheme>(Path.Combine(dir,"theme.xml"));
                if(theme==null || theme.Id<100 || theme.Id.ToString()!=Path.GetFileName(dir) || string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Length>40 || theme.Files==null || theme.Files.Length!=17 || string.IsNullOrEmpty(theme.Files[0]))throw new Exception("皮肤清单无效");
                for(int i=0;i<17;i++)if(theme.Files[i]!=null) {
                    string file=theme.Files[i];
                    if(file!=Path.GetFileName(file) || (Path.GetExtension(file).ToLowerInvariant()!=".ani" && Path.GetExtension(file).ToLowerInvariant()!=".cur"))throw new Exception("光标路径无效");
                    string path=Path.Combine(dir,file);if(!File.Exists(path))throw new Exception("缺少光标："+file);
                }
                Skin skin=(Skin)theme.Id;Imported.Add(skin,theme);Engine.CustomSkins.Add(skin);
            }catch(Exception e){Warnings.Add(Path.GetFileName(dir)+"："+e.Message);}
        }
    }
    public static List<string> Scan(string folder) {
        var files=new List<string>();var pending=new Stack<string>();pending.Push(Path.GetFullPath(folder));int dirs=0;
        while(pending.Count>0) {
            string dir=pending.Pop();if(++dirs>1000)throw new Exception("文件夹层级过多，请选择只包含一款皮肤的文件夹。");
            foreach(string p in Directory.GetFiles(dir)) {
                string ext=Path.GetExtension(p).ToLowerInvariant();
                if((ext==".ani"||ext==".cur") && (File.GetAttributes(p)&FileAttributes.ReparsePoint)==0)files.Add(p);
                if(files.Count>500)throw new Exception("光标超过 500 个，请选择单款皮肤所在文件夹。");
            }
            foreach(string d in Directory.GetDirectories(dir))if((File.GetAttributes(d)&FileAttributes.ReparsePoint)==0)pending.Push(d);
        }
        return files.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
    }
    static string Normalize(string s) {return Regex.Replace(s.ToLowerInvariant(),@"[\s_\-]+","");}
    static readonly string[][] Aliases={
        new[]{"Normal","Normal Select","pointer","arrow","正常选择","普通选择","正常","常规"},new[]{"Help","Help Select","帮助选择","帮助"},
        new[]{"Working","Work","AppStarting","后台运行","后台"},new[]{"Busy","Wait","忙","忙碌"},
        new[]{"Precision","Precision Select","Cross","Crosshair","精确选择"},new[]{"Text","Text Select","IBeam","文本选择","文本"},
        new[]{"Handwriting","Handwrt","NWPen","手写"},new[]{"Unavailable","Unavailiable","No","不可用","禁止"},
        new[]{"Vertical","Vertical Resize","Vert","SizeNS","垂直调整","垂直调整大小"},new[]{"Horizontal","Horizontal Resize","Horz","SizeWE","水平调整","水平调整大小"},
        new[]{"Diagonal1","Diagonal Resize 1","Dgn1","SizeNWSE","斜向调整1"},new[]{"Diagonal2","Diagonal Resize 2","Dgn2","SizeNESW","斜向调整2"},
        new[]{"Move","SizeAll","移动"},new[]{"Alternate","Alternate Select","UpArrow","候选选择","备用选择"},new[]{"Link","Hand","链接选择","链接"},
        new[]{"Pin","Location Select","位置选择"},new[]{"Person","Person Select","人员选择","个人选择"}};
    public static string[] Suggest(string folder,List<string> candidates) {
        var result=new string[17];
        for(int i=0;i<17;i++) {
            var hits=candidates.Where(f=>Aliases[i].Select(Normalize).Contains(Normalize(Path.GetFileNameWithoutExtension(f)))).ToArray();
            if(hits.Length==1)result[i]=hits[0];
        }
        // Read INF declarations as text only. Never install or execute them.
        var infs=candidates.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(d=>Directory.GetFiles(d,"*.inf")).Distinct().ToArray();
        if(infs.Length==1 && new FileInfo(infs[0]).Length<1024*1024) {
            byte[] bytes=File.ReadAllBytes(infs[0]);string text;
            if(bytes.Length>1 && bytes[0]==255 && bytes[1]==254)text=Encoding.Unicode.GetString(bytes);
            else {try{text=new UTF8Encoding(false,true).GetString(bytes);}catch{text=Encoding.GetEncoding(936).GetString(bytes);}}
            var strings=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);bool section=false;
            foreach(string line in text.Split('\n')) {
                string t=line.Trim();if(t.StartsWith("[")){section=t.Equals("[Strings]",StringComparison.OrdinalIgnoreCase);continue;}
                var m=Regex.Match(t,"^([A-Za-z0-9_]+)\\s*=\\s*\"([^\"]+)\"");if(section&&m.Success)strings[m.Groups[1].Value]=m.Groups[2].Value;
            }
            for(int i=0;i<17;i++) {
                var m=Regex.Match(text,"HKCU\\s*,\\s*\"Control Panel\\\\Cursors\"\\s*,\\s*\"?"+Regex.Escape(Engine.Roles[i])+"\"?\\s*,[^\\r\\n]*",RegexOptions.IgnoreCase);
                if(!m.Success)continue;
                var tokens=Regex.Matches(m.Value,@"%([A-Za-z_][A-Za-z0-9_]*)%");
                if(tokens.Count==0)continue;string key=tokens[tokens.Count-1].Groups[1].Value,name;
                if(!strings.TryGetValue(key,out name))continue;
                var hits=candidates.Where(f=>string.Equals(Path.GetFileName(f),name,StringComparison.OrdinalIgnoreCase)).ToArray();if(hits.Length==1)result[i]=hits[0];
            }
        }
        return result;
    }
    public static Skin Import(string folder,string name,string[] mapping) {
        name=(name??"").Trim();
        if(name.Length<1 || name.Length>40 || name.Any(char.IsControl))throw new Exception("请输入 1～40 个字符的皮肤名称。");
        if(mapping==null || mapping.Length!=17 || string.IsNullOrEmpty(mapping[0]))throw new Exception("请为普通选择指定一个光标。");
        string prefix=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        int id;do{id=BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0)&0x7fffffff;}while(id<100 || Directory.Exists(Path.Combine(ImportRoot,id.ToString())));
        Directory.CreateDirectory(ImportRoot);string stage=Path.Combine(ImportRoot,"_pending_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        var theme=new ImportedTheme{Id=id,Name=name,Files=new string[17]};long total=0;
        try {
            for(int i=0;i<17;i++)if(!string.IsNullOrEmpty(mapping[i])) {
                string source=Path.GetFullPath(mapping[i]);if(!source.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("所选光标不在导入文件夹中。");
                var info=new FileInfo(source);if(info.Length>64*1024*1024 || (total+=info.Length)>256*1024*1024)throw new Exception("光标文件过大，单个限 64 MB，整套限 256 MB。");
                string ext=Path.GetExtension(source).ToLowerInvariant();if(ext!=".ani"&&ext!=".cur")throw new Exception("仅支持 ANI 动画和 CUR 静态光标。");
                byte[] data=CursorFormat.ValidateAndNormalize(File.ReadAllBytes(source),ext);
                string filename=Path.GetFileNameWithoutExtension(Engine.Files[i])+ext;
                File.WriteAllBytes(Path.Combine(stage,filename),data);Engine.ValidateCursor(Path.Combine(stage,filename));theme.Files[i]=filename;
            }
            XmlStore.Save(Path.Combine(stage,"theme.xml"),theme);
            Directory.Move(stage,Path.Combine(ImportRoot,id.ToString()));
            Imported.Add((Skin)id,theme);Engine.CustomSkins.Add((Skin)id);return (Skin)id;
        }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
}
public static class CursorFormat {
    static uint U(byte[] b,int p) {return BitConverter.ToUInt32(b,p);}
    static void Icon(byte[] b,int offset,int length,bool allowIcon) {
        if(length<6 || BitConverter.ToUInt16(b,offset)!=0)throw new Exception("CUR 文件头无效。");
        int kind=BitConverter.ToUInt16(b,offset+2),count=BitConverter.ToUInt16(b,offset+4);
        if((kind!=2 && !(allowIcon&&kind==1)) || count<1 || count>256 || 6+count*16>length)throw new Exception("CUR 图像目录无效。");
        for(int i=0;i<count;i++){int p=offset+6+i*16;uint size=U(b,p+8),start=U(b,p+12);if(size<8 || start<6+count*16 || (long)start+size>length)throw new Exception("CUR 图像数据不完整。");}
    }
    public static byte[] ValidateAndNormalize(byte[] bytes,string ext) {
        if(ext==".cur"){Icon(bytes,0,bytes.Length,false);return bytes;}
        if(bytes.Length<12 || Encoding.ASCII.GetString(bytes,0,4)!="RIFF" || Encoding.ASCII.GetString(bytes,8,4)!="ACON")throw new Exception("ANI 文件头无效。");
        uint expected=0,steps=0;int found=0;uint[] sequence=null;int rateCount=-1;
        for(int p=12;p<bytes.Length;) {
            if(p+8>bytes.Length)throw new Exception("ANI 块不完整。");
            uint length=U(bytes,p+4);if((long)p+8+length>bytes.Length)throw new Exception("ANI 长度越界。");
            string tag=Encoding.ASCII.GetString(bytes,p,4);int start=p+8,end=(int)(start+length);
            if(tag=="anih") {if(length<36)throw new Exception("ANI 动画头不完整。");expected=U(bytes,start+4);steps=U(bytes,start+8);}
            if(tag=="seq "){if(length%4!=0)throw new Exception("ANI 帧序列无效。");sequence=new uint[length/4];for(int i=0;i<sequence.Length;i++)sequence[i]=U(bytes,start+i*4);}
            if(tag=="rate"){if(length%4!=0)throw new Exception("ANI 时间表无效。");rateCount=(int)length/4;}
            if(tag=="LIST" && length>=4 && Encoding.ASCII.GetString(bytes,start,4)=="fram") {
                for(int q=start+4;q<end;) {
                    if(q+8>end)throw new Exception("ANI 帧不完整。");uint n=U(bytes,q+4);if((long)q+8+n>end)throw new Exception("ANI 帧长度越界。");
                    if(Encoding.ASCII.GetString(bytes,q,4)=="icon"){Icon(bytes,q+8,(int)n,true);found++;}q+=(int)(8+n+(n%2));
                    if(q>end)throw new Exception("ANI 帧对齐无效。");
                }
            }
            p=(int)(end+(length%2));if(p>bytes.Length)throw new Exception("ANI 块对齐无效。");
        }
        if(expected==0 || expected!=found || steps==0 || steps>100000 || (sequence!=null&&(sequence.Length!=steps||sequence.Any(x=>x>=found))) || (sequence==null&&steps!=expected) || (rateCount>=0&&rateCount!=steps))throw new Exception("ANI 帧数、顺序或时间表不一致。");
        Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length-8),0,bytes,4,4);return bytes;
    }
}
