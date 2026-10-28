using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.ComponentModel;

// A private cursor owned by the preview; never calls SetSystemCursor or writes settings.
public sealed class PreviewCursor:IDisposable {
    public IntPtr Handle {get;private set;}
    public Cursor Cursor {get;private set;}
    public int Pixels {get;private set;}
    public string Path {get;private set;}
    public uint Steps=1,DefaultRate=6;
    public uint[] Rates;
    public PreviewCursor(string path,int pixels,int role) {
        Pixels=pixels;Path=path;
        if(string.IsNullOrEmpty(path)) {
            int id=role==4?32515:role==5?32513:32512;
            Handle=PreviewNative.CopyImage(PreviewNative.LoadCursorW(IntPtr.Zero,new IntPtr(id)),2,pixels,pixels,0);
        }else {
            byte[] data=File.ReadAllBytes(path);
            if(data.Length>=12 && System.Text.Encoding.ASCII.GetString(data,8,4)=="ACON") {
                for(int p=12;p+8<=data.Length;) {
                    int length=BitConverter.ToInt32(data,p+4);
                    if(length<0 || p+8L+length>data.Length)throw new IOException("动画文件结构无效。");
                    string tag=System.Text.Encoding.ASCII.GetString(data,p,4);
                    if(tag=="anih" && length>=36) {
                        Steps=Math.Max(1,BitConverter.ToUInt32(data,p+16));
                        DefaultRate=Math.Max(1,BitConverter.ToUInt32(data,p+36));
                    } else if(tag=="rate" && length%4==0) {
                        Rates=new uint[length/4];for(int i=0;i<Rates.Length;i++)Rates[i]=BitConverter.ToUInt32(data,p+8+i*4);
                    }
                    p+=8+length+(length%2);
                }
            }
            Handle=Native.LoadImageW(IntPtr.Zero,path,2,pixels,pixels,0x10);
        }
        if(Handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"预览无法加载这枚指针。");
        try {Cursor=new Cursor(Handle);}catch{Native.DestroyCursor(Handle);Handle=IntPtr.Zero;throw;}
    }
    public int Delay(uint step) {
        uint rate=Rates!=null&&step<Rates.Length?Rates[step]:DefaultRate;
        return (int)Math.Max(16,Math.Min(60000,rate*1000L/60));
    }
    public void Dispose() {
        if(Cursor!=null){Cursor.Dispose();Cursor=null;}
        if(Handle!=IntPtr.Zero){Native.DestroyCursor(Handle);Handle=IntPtr.Zero;}
    }
}
public static class PreviewNative {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr LoadCursorW(IntPtr instance,IntPtr id);
    [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr CopyImage(IntPtr image,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr GetCursor();
}
public sealed class LivePreview:Control {
    PreviewCursor asset,reference;
    readonly Timer timer=new Timer();
    uint step;
    public int PixelSize {get{return asset==null?0:asset.Pixels;}}
    public IntPtr PreviewHandle {get{return asset==null?IntPtr.Zero:asset.Handle;}}
    public string SourcePath {get{return asset==null?null:asset.Path;}}
    public uint FrameStep {get{return step;}}
    public uint AnimationSteps {get{return asset==null?0:asset.Steps;}}
    public LivePreview() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        BackColor=Color.White;
        timer.Tick+=(s,e)=>{if(asset==null)return;step=(step+1)%asset.Steps;timer.Interval=asset.Delay(step);Invalidate();};
    }
    public void LoadPreview(string path,int pixels,int role) {
        LoadPreview(path,path,pixels,role);
    }
    public void LoadPreview(string path,string referencePath,int pixels,int role) {
        var next=new PreviewCursor(path,pixels,role);
        PreviewCursor nextReference;
        try{nextReference=new PreviewCursor(referencePath,pixels,role);}catch{next.Dispose();throw;}
        var previous=asset;var previousReference=reference;asset=next;reference=nextReference;step=0;Cursor=next.Cursor;
        if(IsHandleCreated && ClientRectangle.Contains(PointToClient(System.Windows.Forms.Cursor.Position)))PreviewNative.SetCursor(next.Handle);
        timer.Stop();timer.Interval=next.Delay(0);if(next.Steps>1)timer.Start();
        if(previous!=null)previous.Dispose();if(previousReference!=null)previousReference.Dispose();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(asset==null)return;
        // Native pixel dimensions, no second stretch by GDI or WinForms.
        IntPtr dc=e.Graphics.GetHdc();
        try{Native.DrawIconEx(dc,(ClientSize.Width-reference.Pixels)/2,(ClientSize.Height-reference.Pixels)/2,reference.Handle,reference.Pixels,reference.Pixels,step,IntPtr.Zero,3);}
        finally{e.Graphics.ReleaseHdc(dc);}
    }
    public void Clear() {
        timer.Stop();Cursor=Cursors.Default;
        if(IsHandleCreated && ClientRectangle.Contains(PointToClient(System.Windows.Forms.Cursor.Position)))PreviewNative.SetCursor(Cursors.Default.Handle);
        if(asset!=null){asset.Dispose();asset=null;}if(reference!=null){reference.Dispose();reference=null;}Invalidate();
    }
    protected override void Dispose(bool disposing) {
        if(disposing){Clear();timer.Dispose();}
        base.Dispose(disposing);
    }
}
