using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;

public class ThemeItem {
    public Skin Skin;
    public override string ToString(){return Engine.Display(Skin)+(Library.Imported.ContainsKey(Skin)?"  · 导入":"");}
}
public class MainWindow:Form {
    readonly Color ink=Color.FromArgb(35,49,70),blue=Color.FromArgb(49,92,135),paper=Color.FromArgb(245,248,252);
    ListBox skins;NumericUpDown scale,gamma;Label title,detail,status,previewInfo,count;Button undo,restore;
    LivePreview preview;ComboBox role;
    readonly Timer editTimer=new Timer{Interval=250};
    bool syncing,checking;
    Skin Selected {get{return skins.SelectedItem==null?Skin.Default:((ThemeItem)skins.SelectedItem).Skin;}}
    public MainWindow(bool cleanPreview=false) {
        SuspendLayout();
        Text="Cursor Skin Manager · 指针管理工具";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        ClientSize=new Size(840,850);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",10);BackColor=paper;ForeColor=ink;
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScroll=true;
        LabelAt("管理你自己的指针",24,18,650,44,22,true);
        LabelAt("选皮肤、用途和大小；鼠标移入白色区域即可临时试用。",26,68,780,26,10,false);
        skins=new ListBox{Bounds=new Rectangle(24,110,244,410),BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=ink,Font=new Font("Microsoft YaHei UI",12),IntegralHeight=false,ItemHeight=45,DrawMode=DrawMode.OwnerDrawFixed,AccessibleName="皮肤列表"};
        skins.DrawItem+=(s,e)=>{if(e.Index<0)return;e.DrawBackground();TextRenderer.DrawText(e.Graphics,skins.Items[e.Index].ToString(),skins.Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-20,e.Bounds.Height),e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
        Controls.Add(skins);skins.SelectedIndexChanged+=(s,e)=>SelectTheme();skins.DoubleClick+=(s,e)=>ApplySelected();
        ButtonAt("＋ 导入新皮肤",24,538,244,40,ImportSkin,blue);
        count=LabelAt("",24,585,244,25,9,false);
        LabelAt("本机色差补偿",24,622,244,30,12,true);
        gamma=new NumericUpDown{Bounds=new Rectangle(24,660,135,34),Minimum=.5m,Maximum=2.5m,Value=1m,DecimalPlaces=2,Increment=.05m,Font=new Font("Microsoft YaHei UI",12),TextAlign=HorizontalAlignment.Center,AccessibleName="色差补偿"};Controls.Add(gamma);
        gamma.ValueChanged+=(s,e)=>QueuePreview();
        gamma.TextChanged+=(s,e)=>QueuePreview();
        ButtonAt("不补偿",171,660,97,34,()=>{gamma.Value=1m;},null);
        LabelAt("1.00 不补偿；偏浅可逐步调高。\n中央图像是原色，移入鼠标对照。\n只在点击应用后保存。",24,704,244,72,9,false);
        LabelAt("导入：ANI / CUR 文件夹\n压缩包请先解压。",24,783,244,45,9,false);
        title=LabelAt("",300,110,500,36,17,true);
        detail=LabelAt("",300,150,500,44,9,false);
        LabelAt("预览用途",300,200,100,26,10,true);
        role=new ComboBox{Bounds=new Rectangle(410,196,390,29),DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="预览用途"};
        role.Items.AddRange(Library.RoleNames);role.SelectedIndex=0;Controls.Add(role);role.SelectedIndexChanged+=(s,e)=>RefreshPreview();
        preview=new LivePreview{Bounds=new Rectangle(300,239,500,270),AccessibleName="动态指针预览区"};Controls.Add(preview);
        previewInfo=LabelAt("",300,516,500,28,9,false);
        LabelAt("预览大小",300,555,250,30,12,true);
        scale=new NumericUpDown{Bounds=new Rectangle(638,550,136,38),Minimum=.5m,Maximum=8m,DecimalPlaces=2,Increment=.05m,Font=new Font("Microsoft YaHei UI",15),TextAlign=HorizontalAlignment.Center,AccessibleName="大小倍数"};Controls.Add(scale);
        scale.ValueChanged+=(s,e)=>QueuePreview();
        scale.TextChanged+=(s,e)=>QueuePreview();
        LabelAt("倍",778,556,30,27,10,false);
        decimal[] presets={1m,1.25m,1.5m,2m,3m};
        for(int i=0;i<presets.Length;i++){decimal n=presets[i];ButtonAt(n.ToString("0.##")+"×",300+i*102,598,92,34,()=>{scale.Value=n;},null);}
        var apply=ButtonAt("使用这款皮肤和大小",300,650,500,42,ApplySelected,blue);AcceptButton=apply;
        undo=ButtonAt("撤销上次更改",300,704,242,34,Undo,null);
        ButtonAt("恢复系统默认和 1×",558,704,242,34,ResetDefault,null);
        restore=ButtonAt("恢复使用本工具前的指针和大小",300,746,500,30,RestoreOriginal,null);
        LabelAt("中央为原始素材；鼠标移入预览区试用所选效果。",300,790,500,27,9,false);
        status=LabelAt("",24,823,780,27,9,true);
        editTimer.Tick+=(s,e)=>{editTimer.Stop();RefreshPreview();};
        ReloadList(cleanPreview?Skin.Default:Engine.CurrentSkin());UpdateState("已就绪；预览不会更改系统设置");
        if(Library.Warnings.Count>0)status.Text="有 "+Library.Warnings.Count+" 款导入皮肤无法加载；请检查其文件是否完整。";
        Activated+=(s,e)=>UpdateState("已就绪");
        Shown+=(s,e)=>RefreshPreview();
        ResumeLayout(false);
    }
    Label LabelAt(string text,int x,int y,int w,int h,float size,bool bold) {
        var l=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),ForeColor=ink,AutoEllipsis=true,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular)};Controls.Add(l);return l;
    }
    Button ButtonAt(string text,int x,int y,int w,int h,Action action,Color? accent) {
        var b=new Button{Text=text,Bounds=new Rectangle(x,y,w,h),FlatStyle=FlatStyle.Flat,BackColor=accent??Color.White,ForeColor=accent.HasValue?Color.White:ink,Font=new Font("Microsoft YaHei UI",9,FontStyle.Bold),Cursor=Cursors.Hand};
        b.FlatAppearance.BorderColor=accent??Color.FromArgb(196,207,219);b.Click+=(s,e)=>action();Controls.Add(b);return b;
    }
    void ReloadList(Skin selected) {
        syncing=true;skins.Items.Clear();foreach(Skin s in Engine.CustomSkins)skins.Items.Add(new ThemeItem{Skin=s});skins.Items.Add(new ThemeItem{Skin=Skin.Default});
        if(selected==Skin.Other)skins.Items.Add(new ThemeItem{Skin=Skin.Other});
        for(int i=0;i<skins.Items.Count;i++)if(((ThemeItem)skins.Items[i]).Skin==selected)skins.SelectedIndex=i;
        if(skins.SelectedIndex<0)skins.SelectedIndex=0;syncing=false;
        count.Text="已导入 "+Library.Imported.Count+" 款 · 不附带素材";SelectTheme();
    }
    void SelectTheme() {
        if(syncing||skins.SelectedItem==null)return;
        try {
            Skin skin=Selected;title.Text=Engine.Display(skin);
            syncing=true;try{var memory=SizeMemory.Load();scale.Value=memory.Get(skin,Engine.CurrentScale());gamma.Enabled=Engine.IsCustom(skin);gamma.Value=gamma.Enabled?memory.Gamma(skin):1m;}finally{syncing=false;}
            int supplied=Engine.IsCustom(skin)?Engine.FilesFor(skin).Count(x=>x!=null):17;
            detail.Text=skin==Skin.Default?"使用 Windows 本机系统指针，不随本工具分发。\n点击左侧导入按钮，添加你自己的 ANI / CUR 皮肤。":skin==Skin.Other?"当前系统正在使用的其他指针方案。":supplied+" 类专用光标"+(supplied<17?"，其余使用系统默认。":"。")+"\n这款皮肤会记住上次应用的大小。";
            RefreshPreview();
        }catch(Exception e){ShowError(e);}
    }
    void RefreshPreview() {
        if(syncing || preview==null || scale==null || role.SelectedIndex<0 || skins.SelectedItem==null)return;
        try {
            decimal selectedScale,selectedGamma;
            if(!decimal.TryParse(scale.Text,out selectedScale)||selectedScale<.5m||selectedScale>8m||!decimal.TryParse(gamma.Text,out selectedGamma)||selectedGamma<.5m||selectedGamma>2.5m) {
                preview.Clear();previewInfo.ForeColor=Color.Firebrick;previewInfo.Text="输入有效数值后预览：大小 0.5～8，补偿 0.50～2.50。";return;
            }
            int dpi=IsHandleCreated?(int)PreviewNative.GetDpiForWindow(Handle):96;
            int pixels=(int)Math.Round(Engine.Pixels(selectedScale)*Math.Max(96,dpi)/96.0);
            preview.LoadPreview(ColorCorrection.PathFor(Selected,role.SelectedIndex,selectedGamma),Engine.Paths(Selected)[role.SelectedIndex],pixels,role.SelectedIndex);
            previewInfo.ForeColor=ink;
            previewInfo.Text="局部预览："+Library.RoleNames[role.SelectedIndex]+" · "+selectedScale.ToString("0.##")+"× · 补偿 "+selectedGamma.ToString("0.00");
        }catch(Exception e){if(checking)throw;preview.Clear();previewInfo.ForeColor=Color.Firebrick;previewInfo.Text="预览未加载："+e.Message;}
    }
    void QueuePreview() {if(syncing)return;editTimer.Stop();editTimer.Start();}
    protected override void Dispose(bool disposing) {if(disposing)editTimer.Dispose();base.Dispose(disposing);}
    void UpdateState(string message) {
        Skin active=Engine.CurrentSkin();string current=Engine.Display(active)+"  "+Engine.CurrentScale().ToString("0.###")+"×";
        status.ForeColor=blue;status.Text=message+"；当前："+current;

        undo.Enabled=History.CanUndo;restore.Enabled=History.CanRestore;
    }
    void ApplySelected() {
        try {
            decimal value;if(!decimal.TryParse(scale.Text,out value)||value<.5m||value>8m)throw new Exception("请输入 0.5～8 之间的倍数。");
            decimal correction;if(!decimal.TryParse(gamma.Text,out correction))throw new Exception("请输入有效的色差补偿值。");
            Engine.Apply(Selected,value,correction);scale.Value=Engine.CurrentScale();UpdateState("已应用并记住大小和颜色补偿");
        }catch(Exception e){ShowError(e);}
    }
    void Undo() {try{if(History.Undo()){ReloadList(Engine.CurrentSkin());UpdateState("已撤销上次更改");}}catch(Exception e){ShowError(e);}}
    void RestoreOriginal() {try{if(History.RestoreOriginal()){ReloadList(Engine.CurrentSkin());UpdateState("已恢复使用前的设置，可撤销");}}catch(Exception e){ShowError(e);}}
    void ResetDefault() {try{Engine.Apply(Skin.Default,1m);ReloadList(Skin.Default);UpdateState("已恢复系统默认和 1×，可撤销");}catch(Exception e){ShowError(e);}}
    void ImportSkin() {
        using(var picker=new FolderBrowserDialog{Description="选择一款皮肤的文件夹（ANI / CUR）；压缩包请先解压。",ShowNewFolderButton=false}) {
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            try {
                var files=Library.Scan(picker.SelectedPath);if(files.Count==0)throw new Exception("这个文件夹没有 ANI 或 CUR 光标文件。");
                using(var dialog=new ImportWindow(picker.SelectedPath,files))if(dialog.ShowDialog(this)==DialogResult.OK) {
                    ReloadList(dialog.ImportedSkin);UpdateState("已导入，点击使用即可应用");
                }
            }catch(Exception e){ShowError(e);}
        }
    }
    void ShowError(Exception e){if(checking)throw new Exception("UI check failed",e);status.ForeColor=Color.Firebrick;status.Text="操作未完成："+e.Message.Split('\n')[0];MessageBox.Show(this,e.Message,"Cursor Skin Manager",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    public void CheckUi(Skin animated,Skin staticTheme,List<string> report) {
        checking=true;
        Action pump=()=>{var clock=System.Diagnostics.Stopwatch.StartNew();while(clock.ElapsedMilliseconds<450){Application.DoEvents();System.Threading.Thread.Sleep(10);}};
        ReloadList(animated);role.SelectedIndex=0;scale.Value=1m;pump();int first=preview.PixelSize;
        string state=Snapshot.Capture().Signature(),memory=SizeMemory.Load().Signature();
        string undoText=File.Exists(History.UndoPath)?File.ReadAllText(History.UndoPath):"";
        scale.Focus();scale.Text="2.50";pump();
        if(preview.PixelSize!=(int)Math.Round(Engine.Pixels(2.5m)*PreviewNative.GetDpiForWindow(Handle)/96.0))throw new Exception("Typed size preview failed");
        gamma.Focus();gamma.Text="1.40";pump();
        if(!preview.SourcePath.Contains(Path.DirectorySeparatorChar+"1.40"+Path.DirectorySeparatorChar))throw new Exception("Typed compensation preview failed");
        PreviewNative.SendMessageW(preview.Handle,0x20,preview.Handle,new IntPtr(1|(0x200<<16)));
        if(PreviewNative.GetCursor()!=preview.PreviewHandle)throw new Exception("Local cursor was not activated");
        PreviewNative.SendMessageW(Handle,0x20,Handle,new IntPtr(1|(0x200<<16)));
        if(PreviewNative.GetCursor()==preview.PreviewHandle)throw new Exception("Local cursor leaked outside preview");
        uint previous=preview.FrameStep;var animationClock=System.Diagnostics.Stopwatch.StartNew();while(preview.FrameStep==previous&&animationClock.ElapsedMilliseconds<1200){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        if(preview.AnimationSteps<2||preview.FrameStep==previous)throw new Exception("Animated preview did not advance");
        role.SelectedIndex=3;if(preview.PreviewHandle==IntPtr.Zero)throw new Exception("Default role fallback preview failed");
        ReloadList(staticTheme);role.SelectedIndex=0;if(!preview.SourcePath.EndsWith(".cur"))throw new Exception("Static CUR preview failed");
        ReloadList(Skin.Default);role.SelectedIndex=4;if(preview.SourcePath!=""||preview.PreviewHandle==IntPtr.Zero)throw new Exception("System crosshair preview failed");
        if(Snapshot.Capture().Signature()!=state||SizeMemory.Load().Signature()!=memory||(File.Exists(History.UndoPath)?File.ReadAllText(History.UndoPath):"")!=undoText)throw new Exception("Preview changed system state or history");
        report.Add("PASS animated/static/fallback previews, typed values, local cursor enter/leave, and unchanged system/history");
    }

}
public class CursorChoice {public string Path,Label;public override string ToString(){return Label;}}
public class ImportWindow:Form {
    readonly string folder;TextBox name;ComboBox[] roles=new ComboBox[17];
    public Skin ImportedSkin;
    public ImportWindow(string source,List<string> files) {
        SuspendLayout();
        folder=source;Text="导入指针皮肤";ClientSize=new Size(660,650);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",9);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        Controls.Add(new Label{Text="皮肤名称",Bounds=new Rectangle(20,20,90,26)});
        name=new TextBox{Text=Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),MaxLength=40,Bounds=new Rectangle(115,16,520,28)};Controls.Add(name);
        Controls.Add(new Label{Text="检查各用途对应的文件；普通选择必填，其余可用系统默认。",Bounds=new Rectangle(20,58,615,32)});
        var panel=new Panel{Bounds=new Rectangle(20,100,620,465),AutoScroll=true};Controls.Add(panel);
        string[] guesses=Library.Suggest(folder,files);
        for(int i=0;i<17;i++) {
            panel.Controls.Add(new Label{Text=Library.RoleNames[i],Bounds=new Rectangle(0,i*27+4,115,23)});
            var box=new ComboBox{Bounds=new Rectangle(120,i*27,477,25),DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=660};roles[i]=box;panel.Controls.Add(box);
            box.Items.Add(new CursorChoice{Path=null,Label=i==0?"（请选择普通选择光标）":"（使用系统默认）"});box.SelectedIndex=0;
            foreach(string file in files){int j=box.Items.Add(new CursorChoice{Path=file,Label=file.Substring(folder.TrimEnd(Path.DirectorySeparatorChar).Length+1)});if(file==guesses[i])box.SelectedIndex=j;}
        }
        Controls.Add(new Label{Text="导入会复制所选素材；成功后可删除原下载文件夹。",Bounds=new Rectangle(20,573,615,25)});
        var import=new Button{Text="导入皮肤",Bounds=new Rectangle(410,608,105,30)};Controls.Add(import);AcceptButton=import;
        var cancel=new Button{Text="取消",Bounds=new Rectangle(530,608,105,30),DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;
        import.Click+=(s,e)=>{try{ImportedSkin=Library.Import(folder,name.Text,roles.Select(b=>((CursorChoice)b.SelectedItem).Path).ToArray());DialogResult=DialogResult.OK;Close();}catch(Exception error){MessageBox.Show(this,error.Message,"无法导入",MessageBoxButtons.OK,MessageBoxIcon.Information);}};
        ResumeLayout(false);
    }
}

