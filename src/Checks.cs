using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;

// All cursor fixtures are generated from simple geometry at test time.
// No downloaded or third-party cursor material is used, embedded, or distributed.
public static class Checks {
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    public static int Run(string report) {
        var before=Snapshot.Capture();var lines=new List<string>();
        try {
            Assert(Assembly.GetExecutingAssembly().GetManifestResourceNames().Length==0,"Unexpected embedded resources");
            Assert(Library.Imported.Count==0&&Engine.CustomSkins.Count==0,"Fresh library is not empty");
            lines.Add("PASS zero embedded cursor resources and empty fresh library");
            string a=Path.Combine(Engine.Root,"synthetic-a"),b=Path.Combine(Engine.Root,"synthetic-b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
            byte[] cur=StaticCursor(70,150,220),ani=AnimatedCursor();
            File.WriteAllBytes(Path.Combine(a,"Normal.ani"),ani);File.WriteAllBytes(Path.Combine(b,"Normal.cur"),cur);
            string state=Snapshot.Capture().Signature();
            Skin first=Library.Import(a,"Test animated",Library.Suggest(a,Library.Scan(a)));
            Skin second=Library.Import(b,"Test static",Library.Suggest(b,Library.Scan(b)));
            Assert(Snapshot.Capture().Signature()==state,"Import changed system settings");
            Directory.Move(a,a+"-moved");Directory.Move(b,b+"-moved");Library.Load();
            Assert(Library.Imported.Count==2&&File.Exists(Engine.Paths(first)[0]),"Import depends on original folder");
            Assert(Engine.Paths(first).Skip(1).SequenceEqual(Engine.Paths(Skin.Default).Skip(1)),"Missing roles did not fall back");
            lines.Add("PASS ANI/CUR import, source-independent copies, catalog reload, and missing-role defaults");
            foreach(decimal value in new[]{.5m,1.5m,2.25m,8m}){Engine.Apply(first,value);Engine.Verify(first,Engine.Pixels(value),Engine.Paths(first));}
            Engine.Apply(first,1.5m,1.4m);Engine.Apply(second,2m);
            Assert(SizeMemory.Load().Get(first,1m)==1.5m&&SizeMemory.Load().Gamma(first)==1.4m,"Per-theme preferences failed");
            Assert(History.Undo()&&Engine.CurrentSkin()==first&&Engine.CurrentScale()==1.5m&&SizeMemory.Load().Gamma(first)==1.4m,"Undo failed");
            Assert(File.ReadAllBytes(Path.Combine(Engine.AssetDir(first),"Normal.ani")).SequenceEqual(ani),"Compensation changed original");
            lines.Add("PASS size bounds/fractions, per-theme preferences, compensation, original preservation and undo");
            Engine.Apply(Skin.Default,1m);Assert(History.Undo()&&Engine.CurrentSkin()==first,"Default reset undo failed");
            Engine.Apply(first,1.5m,1.4m);
            string snapshot=Snapshot.Capture().Signature(),memory=SizeMemory.Load().Signature(),history=File.ReadAllText(History.UndoPath);
            string missing=Engine.Paths(second)[0];File.Move(missing,missing+".held");
            try{bool rejected=false;try{Engine.Apply(second,1m);}catch(FileNotFoundException){rejected=true;}Assert(rejected,"Missing cursor accepted");}finally{File.Move(missing+".held",missing);}
            Assert(Snapshot.Capture().Signature()==snapshot&&SizeMemory.Load().Signature()==memory&&File.ReadAllText(History.UndoPath)==history,"Failure damaged state");
            File.WriteAllBytes(Path.Combine(a+"-moved","broken.ani"),new byte[]{1,2,3});var mapping=new string[17];mapping[0]=Path.Combine(a+"-moved","broken.ani");
            int count=Library.Imported.Count;bool blocked=false;try{Library.Import(a+"-moved","Invalid",mapping);}catch{blocked=true;}
            Assert(blocked&&Library.Imported.Count==count,"Invalid import was published");
            lines.Add("PASS reset/undo and failed apply/import preserve state and history");
            using(var form=new MainWindow()){form.Show();Application.DoEvents();form.CheckUi(first,second,lines);form.Close();}
            Assert(History.RestoreOriginal()&&Snapshot.Capture().Signature()==before.Signature(),"Original restore failed");
            lines.Add("PASS restore original cursor and size after catalog changes");
        } finally {
            before.Restore();Assert(Snapshot.Capture().Signature()==before.Signature(),"Verification did not restore Windows state");
            lines.Add("PASS exact pre-test Windows settings restored");File.WriteAllLines(report,lines);
        }
        return 0;
    }
    static byte[] StaticCursor(byte red,byte green,byte blue) {
        const int edge=32,row=4,bitmapLength=40+edge*edge*4+row*edge;
        using(var m=new MemoryStream())using(var w=new BinaryWriter(m)) {
            w.Write((ushort)0);w.Write((ushort)2);w.Write((ushort)1);w.Write((byte)edge);w.Write((byte)edge);w.Write((ushort)0);
            w.Write((ushort)1);w.Write((ushort)1);w.Write(bitmapLength);w.Write(22);
            w.Write(40);w.Write(edge);w.Write(edge*2);w.Write((ushort)1);w.Write((ushort)32);w.Write(0);w.Write(edge*edge*4+row*edge);w.Write(new byte[16]);
            for(int y=0;y<edge;y++)for(int x=0;x<edge;x++){bool visible=x>=8&&x<24&&y>=8&&y<24;w.Write(visible?blue:(byte)0);w.Write(visible?green:(byte)0);w.Write(visible?red:(byte)0);w.Write(visible?(byte)255:(byte)0);}
            for(int y=0;y<edge;y++){uint mask=y>=8&&y<24?0xff0000ffu:0xffffffffu;w.Write(mask);}
            return m.ToArray();
        }
    }
    static void Chunk(BinaryWriter w,string tag,byte[] data){w.Write(Encoding.ASCII.GetBytes(tag));w.Write(data.Length);w.Write(data);if((data.Length&1)!=0)w.Write((byte)0);}
    static byte[] AnimatedCursor() {
        byte[] header;using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){foreach(int n in new[]{36,3,3,32,32,32,1,20,1})w.Write(n);header=m.ToArray();}
        byte[] frames;using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(Encoding.ASCII.GetBytes("fram"));Chunk(w,"icon",StaticCursor(200,90,70));Chunk(w,"icon",StaticCursor(60,170,110));Chunk(w,"icon",StaticCursor(80,100,220));frames=m.ToArray();}
        using(var m=new MemoryStream())using(var w=new BinaryWriter(m)) {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(0);w.Write(Encoding.ASCII.GetBytes("ACON"));Chunk(w,"anih",header);Chunk(w,"LIST",frames);
            byte[] data=m.ToArray();Array.Copy(BitConverter.GetBytes(data.Length-8),0,data,4,4);return data;
        }
    }
}
