using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Microsoft.Win32;

// Imported themes use generated IDs >= 100; no built-in artwork is shipped.
public enum Skin { Other=-1, Default=0 }

public static class Native {
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool SystemParametersInfoW(uint action,uint param,IntPtr value,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool SendNotifyMessageW(IntPtr window,uint message,IntPtr param,string section);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr LoadImageW(IntPtr instance,string path,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")] public static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,uint step,IntPtr brush,uint flags);
}

public class SavedValue {
    public string Key,Name,Text;
    public bool Exists;
    public RegistryValueKind Kind;
    public int Number;
}

public class Snapshot {
    public List<SavedValue> Values=new List<SavedValue>();
    public static Snapshot Capture() {
        var s=new Snapshot();
        foreach(string name in Engine.Roles.Concat(new[]{"","Scheme Source","CursorBaseSize"})) s.Read(Engine.CursorKey,name);
        s.Read(Engine.AccessibilityKey,"CursorSize");s.Read(Engine.AccessibilityKey,"CursorType");
        s.Read(Engine.CursorKey+@"\Schemes",Engine.Scheme(Skin.Default));
        return s;
    }
    void Read(string key,string name) {
        var v=new SavedValue {Key=key,Name=name};
        using(var k=Registry.CurrentUser.OpenSubKey(key)) {
            v.Exists=k!=null && k.GetValueNames().Contains(name);
            if(v.Exists) {
                v.Kind=k.GetValueKind(name);
                object raw=k.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                if(v.Kind==RegistryValueKind.DWord) v.Number=(int)raw;
                else if(v.Kind==RegistryValueKind.String || v.Kind==RegistryValueKind.ExpandString) v.Text=(string)raw;
                else throw new InvalidOperationException("不支持的指针设置类型："+name);
            }
        }
        Values.Add(v);
    }
    public int BaseSize { get {var v=Values.First(x=>x.Key==Engine.CursorKey && x.Name=="CursorBaseSize");return v.Exists?v.Number:32;} }
    public void Save(string path) {
        string tmp=path+".tmp";
        using(var stream=File.Create(tmp)) new XmlSerializer(typeof(Snapshot)).Serialize(stream,this);
        if(File.Exists(path)) File.Replace(tmp,path,null);else File.Move(tmp,path);
    }
    public static Snapshot Load(string path) {var s=XmlStore.Load<Snapshot>(path);s.Validate();return s;}
    public void Validate() {
        var expected=Capture().Values.Select(x=>x.Key+"\0"+x.Name).OrderBy(x=>x).ToArray();
        if(Values==null || Values.Any(x=>x==null) || !Values.Select(x=>x.Key+"\0"+x.Name).OrderBy(x=>x).SequenceEqual(expected))throw new Exception("备份内容不完整，未修改当前设置。");
        foreach(var v in Values.Where(x=>x.Exists)) {
            bool number=v.Name=="CursorBaseSize"||v.Name=="CursorSize"||v.Name=="CursorType"||v.Name=="Scheme Source";
            if(number && v.Kind!=RegistryValueKind.DWord)throw new Exception("备份中的数值类型无效。");
            if(!number && (v.Text==null || (v.Kind!=RegistryValueKind.String && v.Kind!=RegistryValueKind.ExpandString)))throw new Exception("备份中的路径类型无效。");
        }
        if(BaseSize<1 || BaseSize>256)throw new Exception("备份中的大小无效。");
    }
    public string Signature() {using(var w=new StringWriter()) {new XmlSerializer(typeof(Snapshot)).Serialize(w,this);return w.ToString();}}
    void WriteValues() {
        foreach(var v in Values) using(var k=Registry.CurrentUser.CreateSubKey(v.Key)) {
            if(v.Exists) k.SetValue(v.Name,v.Kind==RegistryValueKind.DWord?(object)v.Number:(object)v.Text,v.Kind);
            else k.DeleteValue(v.Name,false);
        }
    }
    public void Restore() {
        WriteValues();Engine.Refresh(BaseSize);
        // Refresh may write CursorBaseSize; restore absence/type as well as values.
        WriteValues();
    }
    public void ValidateFiles() {
        foreach(var v in Values.Where(x=>x.Key==Engine.CursorKey && Engine.Roles.Contains(x.Name) && x.Exists)) {
            string path=Environment.ExpandEnvironmentVariables(v.Text??"");
            if(path.Length>0 && !File.Exists(path)) throw new FileNotFoundException("之前的指针文件已不存在，可改用“恢复默认和 1×”。",path);
        }
    }
}

public static class Engine {
    public const string CursorKey=@"Control Panel\Cursors", AccessibilityKey=@"Software\Microsoft\Accessibility";
    public static readonly List<Skin> CustomSkins=new List<Skin>();
    public static bool IsCustom(Skin skin) {return CustomSkins.Contains(skin);}
    public static string Scheme(Skin skin) {return "Cursor Skin Manager";}
    // Documents is shared with Explorer, unlike a packaged app's redirected LocalAppData.
    public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"CursorSkinManager");
    public static bool RecordBackups=true;
    public static readonly string[] Roles={"Arrow","Help","AppStarting","Wait","Crosshair","IBeam","NWPen","No","SizeNS","SizeWE","SizeNWSE","SizeNESW","SizeAll","UpArrow","Hand","Pin","Person"};
    public static readonly string[] Files={"Normal.ani","Help.ani","Working.ani","Busy.ani","Precision.ani","Text.ani","Handwriting.ani","Unavailable.ani","Vertical.ani","Horizontal.ani","Diagonal1.ani","Diagonal2.ani","Move.ani","Alternate.ani","Link.ani","Pin.ani","Person.ani"};
    static readonly string[] DefaultFiles={"aero_arrow.cur","aero_helpsel.cur","aero_working.ani","aero_busy.ani","","","aero_pen.cur","aero_unavail.cur","aero_ns.cur","aero_ew.cur","aero_nwse.cur","aero_nesw.cur","aero_move.cur","aero_up.cur","aero_link.cur","aero_pin.cur","aero_person.cur"};
    public static string AssetDir(Skin skin) {
        if(!IsCustom(skin))throw new ArgumentOutOfRangeException("skin");
        return Path.Combine(Root,"imports",((int)skin).ToString(CultureInfo.InvariantCulture));
    }
    public static string OriginalPath {get{return Path.Combine(Root,"original-settings.xml");}}
    public static string Display(Skin skin) {
        ImportedTheme theme;if(Library.Imported.TryGetValue(skin,out theme))return theme.Name;
        return skin==Skin.Default?"Windows 默认":"当前系统方案";
    }
    public static string[] FilesFor(Skin skin) {
        ImportedTheme theme;if(Library.Imported.TryGetValue(skin,out theme))return theme.Files;
        throw new ArgumentOutOfRangeException("skin","请先导入指针皮肤。");
    }
    public static void Prepare() {Directory.CreateDirectory(Root);}
    public static void ValidateCursor(string path) {
        IntPtr handle=Native.LoadImageW(IntPtr.Zero,path,2,48,48,0x10);
        if(handle==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows 无法读取指针："+path);
        Native.DestroyCursor(handle);
    }
    public static int Pixels(decimal scale) {
        if(scale<.5m || scale>8m) throw new ArgumentOutOfRangeException("scale","请输入 0.5～8 之间的倍数。");
        return (int)Math.Round(scale*32m,MidpointRounding.AwayFromZero);
    }
    public static decimal CurrentScale() {
        using(var k=Registry.CurrentUser.OpenSubKey(CursorKey)) return Convert.ToDecimal(k==null?32:k.GetValue("CursorBaseSize",32))/32m;
    }
    public static Skin CurrentSkin() {
        using(var k=Registry.CurrentUser.OpenSubKey(CursorKey)) {
            string name=Convert.ToString(k==null?"":k.GetValue(""));
            string arrow=Convert.ToString(k==null?"":k.GetValue("Arrow"));
            Skin corrected=ColorCorrection.Recognize(arrow);if(corrected!=Skin.Other)return corrected;
            foreach(Skin skin in CustomSkins) if(string.Equals(arrow,Path.Combine(AssetDir(skin),FilesFor(skin)[0]),StringComparison.OrdinalIgnoreCase))return skin;
            if(name=="@main.cpl,-1020" || name=="Windows Default") return Skin.Default;
            return Skin.Other;
        }
    }
    public static void Refresh(int size) {
        if(!Native.SystemParametersInfoW(0x57,0,IntPtr.Zero,2)) throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows 未能载入指针样式。");
        if(!Native.SystemParametersInfoW(0x2029,0,new IntPtr(size),3)) throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows 未能应用大小。");
        Native.SendNotifyMessageW(new IntPtr(0xffff),0x1a,IntPtr.Zero,CursorKey);
        Native.SendNotifyMessageW(new IntPtr(0xffff),0x1a,IntPtr.Zero,AccessibilityKey);
    }
    public static string[] Paths(Skin skin) {
        if(IsCustom(skin)) {
            var defaults=Paths(Skin.Default);
            return FilesFor(skin).Select((file,i)=>file==null?defaults[i]:Path.Combine(AssetDir(skin),file)).ToArray();
        }
        if(skin==Skin.Default) return DefaultFiles.Select(x=>x.Length==0?"":Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Cursors",x)).ToArray();
        using(var k=Registry.CurrentUser.OpenSubKey(CursorKey)) return Roles.Select(x=>Convert.ToString(k==null?"":k.GetValue(x,""))).ToArray();
    }
    public static void Apply(Skin skin,decimal scale) {Apply(skin,scale,1m);}
    public static void Apply(Skin skin,decimal scale,decimal gamma) {
        int size=Pixels(scale);
        ColorCorrection.Validate(gamma);if(!IsCustom(skin))gamma=1m;
        if(!IsCustom(skin) && skin!=Skin.Default && skin!=Skin.Other) throw new ArgumentOutOfRangeException("skin");
        string[] paths=ColorCorrection.PathsFor(skin,gamma);
        foreach(string path in paths) if(path.Length>0) {
            if(!File.Exists(path)) throw new FileNotFoundException("找不到指针文件。请检查导入的皮肤文件是否完整。",path);
            ValidateCursor(path);
        }
        var before=Snapshot.Capture();
        var memoryBefore=SizeMemory.Load();
        try {
            using(var k=Registry.CurrentUser.CreateSubKey(CursorKey)) {
                if(skin!=Skin.Other) {
                    for(int i=0;i<Roles.Length;i++) k.SetValue(Roles[i],paths[i],RegistryValueKind.ExpandString);
                    k.SetValue("",IsCustom(skin)?Scheme(skin):"@main.cpl,-1020",RegistryValueKind.String);
                    k.SetValue("Scheme Source",skin==Skin.Default?2:1,RegistryValueKind.DWord);
                }
                k.SetValue("CursorBaseSize",size,RegistryValueKind.DWord);
            }
            if(IsCustom(skin)) using(var k=Registry.CurrentUser.CreateSubKey(CursorKey+@"\Schemes"))
                k.SetValue(Scheme(skin),string.Join(",",paths),RegistryValueKind.String);
            using(var k=Registry.CurrentUser.CreateSubKey(AccessibilityKey)) {
                k.SetValue("CursorSize",Math.Max(1,Math.Min(15,(int)Math.Round((size-32)/16.0)+1)),RegistryValueKind.DWord);
                if(skin!=Skin.Other) k.SetValue("CursorType",0,RegistryValueKind.DWord);
            }
            Refresh(size);
            Verify(skin,size,paths);
            SizeMemory.Remember(skin,size/32m,gamma);
            if(RecordBackups)History.Commit(before,memoryBefore);
        } catch(Exception ex) {
            try {before.Restore();memoryBefore.Save();} catch(Exception rollback) {throw new Exception(ex.Message+"\n恢复失败："+rollback.Message+"\n备份位置："+Root,ex);}
            throw new Exception(ex.Message+"\n已恢复修改前的设置。",ex);
        }
    }
    public static void Verify(Skin skin,int size,string[] paths) {
        using(var k=Registry.CurrentUser.OpenSubKey(CursorKey)) {
            if(Convert.ToInt32(k.GetValue("CursorBaseSize"))!=size) throw new Exception("指针大小未保存。");
            for(int i=0;i<Roles.Length;i++) if(Convert.ToString(k.GetValue(Roles[i],""))!=paths[i]) throw new Exception("指针设置不完整："+Roles[i]);
        }
        if(skin!=Skin.Other && CurrentSkin()!=skin) throw new Exception("皮肤名称未保存。");
    }
    public static bool RestoreOriginal() {return History.RestoreOriginal();}
}
