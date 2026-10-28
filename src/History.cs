using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;

public static class XmlStore {
    public static T Load<T>(string path) {
        using(var r=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024}))
            return (T)new XmlSerializer(typeof(T)).Deserialize(r);
    }
    public static void Save<T>(string path,T value) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var f=File.Create(temp))new XmlSerializer(typeof(T)).Serialize(f,value);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        } finally {if(File.Exists(temp))File.Delete(temp);}
    }
}
public class SkinScale {public int Id;public decimal Scale;public decimal Gamma=1m;}
public class SizeMemory {
    public List<SkinScale> Values=new List<SkinScale>();
    public static string FilePath {get{return Path.Combine(Engine.Root,"sizes.xml");}}
    public static SizeMemory Load() {var m=File.Exists(FilePath)?XmlStore.Load<SizeMemory>(FilePath):new SizeMemory();m.Validate();return m;}
    public void Validate() {
        if(Values==null || Values.Any(x=>x==null || x.Scale<.5m || x.Scale>8m || x.Gamma<.5m || x.Gamma>2.5m) || Values.Select(x=>x.Id).Distinct().Count()!=Values.Count)throw new Exception("大小或颜色记录内容无效，已保留原文件："+FilePath);
    }
    public void Save() {Validate();XmlStore.Save(FilePath,this);}
    public decimal Get(Skin skin,decimal fallback) {var v=Values.FirstOrDefault(x=>x.Id==(int)skin);return v==null?Math.Max(.5m,Math.Min(8m,fallback)):v.Scale;}
    public decimal Gamma(Skin skin) {var v=Values.FirstOrDefault(x=>x.Id==(int)skin);return v==null?1m:v.Gamma;}
    public static void Remember(Skin skin,decimal scale) {Remember(skin,scale,1m);}
    public static void Remember(Skin skin,decimal scale,decimal gamma) {
        var m=Load();var v=m.Values.FirstOrDefault(x=>x.Id==(int)skin);
        if(v==null){v=new SkinScale{Id=(int)skin};m.Values.Add(v);}v.Scale=scale;v.Gamma=gamma;m.Save();
    }
    public string Signature() {return string.Join(";",Values.OrderBy(x=>x.Id).Select(x=>x.Id+":"+x.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+x.Gamma.ToString(System.Globalization.CultureInfo.InvariantCulture)));}
}
public class ActionState {public Snapshot Cursors;public SizeMemory Sizes;}
public static class History {
    public static string UndoPath {get{return Path.Combine(Engine.Root,"undo.xml");}}
    public static bool CanUndo {get{return File.Exists(UndoPath);}}
    public static bool CanRestore {get{return File.Exists(Engine.OriginalPath);}}
    public static void Commit(Snapshot before,SizeMemory memory) {
        var state=new ActionState{Cursors=before,Sizes=memory};
        if(!File.Exists(Engine.OriginalPath))XmlStore.Save(Engine.OriginalPath,state);
        XmlStore.Save(UndoPath,state);
    }
    static ActionState Load(string path) {
        var state=XmlStore.Load<ActionState>(path);
        if(state==null || state.Cursors==null || state.Sizes==null)throw new Exception("恢复记录不完整。");
        state.Cursors.Validate();state.Cursors.ValidateFiles();state.Sizes.Validate();return state;
    }
    public static bool Undo() {return Restore(UndoPath,true);}
    public static bool RestoreOriginal() {return Restore(Engine.OriginalPath,false);}
    static bool Restore(string path,bool consume) {
        if(!File.Exists(path))return false;
        var wanted=Load(path);var before=Snapshot.Capture();var memory=SizeMemory.Load();
        try {
            wanted.Cursors.Restore();wanted.Sizes.Save();
            if(Snapshot.Capture().Signature()!=wanted.Cursors.Signature())throw new Exception("恢复结果验证失败。");
            if(consume)File.Delete(UndoPath);else Commit(before,memory);
            return true;
        } catch(Exception error) {
            try{before.Restore();memory.Save();}catch(Exception rollback){throw new Exception(error.Message+"\n还原本次操作失败："+rollback.Message);}
            throw;
        }
    }
}
