using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;

// Local compensation is written only to a content-addressed cache. Originals stay byte-identical.
public static class ColorCorrection {
    public static void Validate(decimal value) {
        if(value<.5m||value>2.5m)throw new ArgumentOutOfRangeException("value","色差补偿请输入 0.50～2.50；1.00 为不补偿。");
    }
    public static string Root {get{return System.IO.Path.Combine(Engine.Root,"color-v1");}}
    public static string PathFor(Skin skin,int role,decimal gamma) {
        Validate(gamma);string original=Engine.Paths(skin)[role];
        if(gamma==1m || !Engine.IsCustom(skin) || Engine.FilesFor(skin)[role]==null)return original;
        byte[] input=File.ReadAllBytes(original);string hash;
        using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
        string folder=System.IO.Path.Combine(Root,((int)skin).ToString(CultureInfo.InvariantCulture),gamma.ToString("0.00",CultureInfo.InvariantCulture));
        string output=System.IO.Path.Combine(folder,hash+System.IO.Path.GetExtension(original));
        if(!File.Exists(output)) {
            byte[] adjusted=Transform(input,gamma);Directory.CreateDirectory(folder);string temporary=output+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temporary,adjusted);Engine.ValidateCursor(temporary);File.Move(temporary,output);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        return output;
    }
    public static string[] PathsFor(Skin skin,decimal gamma) {return Enumerable.Range(0,17).Select(i=>PathFor(skin,i,gamma)).ToArray();}
    public static Skin Recognize(string path) {
        string prefix=Root+System.IO.Path.DirectorySeparatorChar;
        if(path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) {
            string id=path.Substring(prefix.Length).Split(System.IO.Path.DirectorySeparatorChar)[0];int n;
            if(int.TryParse(id,out n)&&Engine.IsCustom((Skin)n))return (Skin)n;
        }
        return Skin.Other;
    }
    public static byte[] Transform(byte[] source,decimal gamma) {
        Validate(gamma);var result=(byte[])source.Clone();if(gamma==1m)return result;
        var map=new byte[256];for(int i=0;i<256;i++)map[i]=(byte)Math.Round(255*Math.Pow(i/255.0,(double)gamma));
        if(result.Length>=12 && Tag(result,0)=="RIFF" && Tag(result,8)=="ACON")Chunks(result,12,result.Length,map);
        else Cur(result,0,result.Length,map);
        return result;
    }
    static string Tag(byte[] b,int p) {return System.Text.Encoding.ASCII.GetString(b,p,4);}
    static void Chunks(byte[] data,int start,int end,byte[] map) {
        for(int p=start;p+8<=end;) {
            int size=BitConverter.ToInt32(data,p+4);if(size<0||p+8L+size>end)throw new IOException("动画资源边界无效。");
            string tag=Tag(data,p);
            if(tag=="icon")Cur(data,p+8,size,map);
            else if(tag=="LIST"&&size>=4&&Tag(data,p+8)=="fram")Chunks(data,p+12,p+8+size,map);
            p+=8+size+(size&1);
        }
    }
    static void Cur(byte[] data,int start,int length,byte[] map) {
        if(length<6||BitConverter.ToUInt16(data,start+2)!=2)throw new IOException("补偿只支持 CUR / ANI 光标。");
        int count=BitConverter.ToUInt16(data,start+4);
        if(count<1||6+16L*count>length)throw new IOException("光标目录无效。");
        for(int i=0;i<count;i++) {
            int entry=start+6+i*16,n=BitConverter.ToInt32(data,entry+8),offset=BitConverter.ToInt32(data,entry+12);
            if(offset<6+16*count||n<40||offset+(long)n>length)throw new IOException("光标图像边界无效。");
            int p=start+offset,header=BitConverter.ToInt32(data,p);
            if(header!=40&&header!=108&&header!=124)throw new IOException("此导入素材暂不支持色差补偿，请将补偿设为 1.00 后使用。");
            if(header>n||BitConverter.ToInt32(data,p+16)!=0)throw new IOException("此压缩素材暂不支持色差补偿，请将补偿设为 1.00 后使用。");
            int width=BitConverter.ToInt32(data,p+4),doubleHeight=BitConverter.ToInt32(data,p+8),bits=BitConverter.ToUInt16(data,p+14);
            if(width<1||width>256||doubleHeight==0||Math.Abs((long)doubleHeight)>512||doubleHeight%2!=0)throw new IOException("光标尺寸无效。");
            int height=Math.Abs(doubleHeight)/2;
            if(bits==1)continue; // Preserve monochrome inversion/mask semantics.
            if(bits==4||bits==8) {
                int colors=BitConverter.ToInt32(data,p+32);if(colors==0)colors=1<<bits;
                if(colors<1||colors>(1<<bits)||header+colors*4L>n)throw new IOException("调色板无效。");
                for(int c=0;c<colors;c++)for(int channel=0;channel<3;channel++){int q=p+header+c*4+channel;data[q]=map[data[q]];}
            } else if(bits==24||bits==32) {
                int stride=((width*bits+31)/32)*4,bytes=bits/8;
                if(header+(long)stride*height>n)throw new IOException("光标像素不完整。");
                for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                    int q=p+header+y*stride+x*bytes;
                    for(int c=0;c<3;c++)data[q+c]=map[data[q+c]];
                }
            }else throw new IOException("此色深暂不支持补偿，请将补偿设为 1.00 后使用。");
        }
    }
}
