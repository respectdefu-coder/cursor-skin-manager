using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Threading;
using System.Drawing;

[assembly:AssemblyTitle("Cursor Skin Manager")]
[assembly:AssemblyDescription("Import and manage your own Windows cursor themes. No cursor artwork included.")]
[assembly:AssemblyVersion("1.0.0.0")]
public static class Program {
    [STAThread] public static int Main(string[] args) {
        bool test=args.Length==2&&args[0]=="--self-test";
        bool render=args.Length==2&&args[0]=="--render";
        try {
            if(test||render)Engine.Root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])),"local-test-"+Guid.NewGuid().ToString("N"));
            using(var gate=new Mutex(false,"Local\\CursorSkinManager-"+Environment.UserName+(test||render?"-check":""))) {
                bool owns;try{owns=gate.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
                if(!owns){MessageBox.Show("工具已经打开，请切回已有窗口。","Cursor Skin Manager");return 2;}
                try {
                    Engine.Prepare();Library.Load();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                    if(test)return Checks.Run(args[1]);
                    if(render) {
                        using(var form=new MainWindow(true)){form.Show();Application.DoEvents();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(args[1]);}form.Close();}return 0;
                    }
                    Application.Run(new MainWindow());return 0;
                }finally{gate.ReleaseMutex();}
            }
        }catch(Exception e) {
            if(test||render)File.WriteAllText(args[1]+".error.txt",e.ToString());
            else MessageBox.Show(e.Message,"Cursor Skin Manager",MessageBoxButtons.OK,MessageBoxIcon.Error);
            return 1;
        }
    }
}
