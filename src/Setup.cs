using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly:AssemblyVersion("1.2.2.0")]
[assembly:AssemblyFileVersion("1.2.2.0")]

static class Setup {
    static string Home {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlameCursor");}}
    [STAThread] static int Main(string[] args) {
        try {
            if(args.Length>0&&args[0]=="--remove") {Remove(args.Length>1?int.Parse(args[1]):0);return 0;}
            if(args.Length>0&&args[0]=="--uninstall") {
                Application.EnableVisualStyles();
                if(MessageBox.Show("Remove Flame Cursor and restore your Windows pointer?","Uninstall Flame Cursor",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
                string worker=Path.Combine(Path.GetTempPath(),"FlameCursor-Remove-"+Guid.NewGuid().ToString("N")+".exe");
                File.Copy(Assembly.GetExecutingAssembly().Location,worker);
                Process.Start(new ProcessStartInfo(worker,"--remove "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true});return 0;
            }
            if(args.Length>0&&args[0]=="--install-silent") {Install();return 0;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var form=new Form{Text="Install Flame Cursor",ClientSize=new Size(530,290),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,StartPosition=FormStartPosition.CenterScreen,BackColor=Color.FromArgb(20,28,28),ForeColor=Color.White,Font=new Font("Segoe UI",10)};
            var title=new Label{Text="Flame Cursor",Font=new Font("Segoe UI",23),Location=new Point(28,23),AutoSize=true};form.Controls.Add(title);
            form.Controls.Add(new Label{Text="An animated Windows pointer with independent arrow and flame controls.\n\nInstalls for your account only. Adds Cursor Settings to the desktop right-click menu and Start menu.\n\nWindows 10 and 11 · No administrator access required",Location=new Point(30,80),Size=new Size(470,145)});
            var button=new Button{Text="Install",Location=new Point(345,235),Size=new Size(150,34),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(44,71,55)};form.Controls.Add(button);
            button.Click+=delegate {button.Enabled=false;try{Install();Process.Start(new ProcessStartInfo(Path.Combine(Home,"FlameCursor.exe")){UseShellExecute=true});form.Close();}catch(Exception e){button.Enabled=true;MessageBox.Show(e.Message,"Installation could not finish");}};
            Application.Run(form);return 0;
        }catch(Exception e){try{File.WriteAllText(Path.Combine(Path.GetTempPath(),"FlameCursor-setup-error.txt"),e.ToString());}catch{}return 1;}
    }
    static void Extract(string resource,string output) {
        using(Stream input=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
            if(input==null)throw new IOException("Installer resource missing: "+resource);
            using(FileStream file=File.Create(output+".new"))input.CopyTo(file);
            if(File.Exists(output))File.Delete(output);File.Move(output+".new",output);
        }
    }
    static void Install() {
        Directory.CreateDirectory(Home);
        string previous=Path.Combine(Home,"FlameCursor.exe");
        if(File.Exists(previous)) {
            var version=FileVersionInfo.GetVersionInfo(previous);
            if(version.FileMajorPart>1||version.FileMajorPart==1&&version.FileMinorPart>=2) {
                var stop=Process.Start(new ProcessStartInfo(previous,"--stop-physics"){UseShellExecute=false,CreateNoWindow=true});stop.WaitForExit();
                if(stop.ExitCode!=0)throw new IOException("Could not stop the reactive flame. Please disable it and try again.");
            }
        }
        Extract("CursorApp",Path.Combine(Home,"FlameCursor.exe"));Extract("Guide",Path.Combine(Home,"READ ME.txt"));
        string current=Assembly.GetExecutingAssembly().Location, uninstaller=Path.Combine(Home,"Uninstall.exe");
        if(!string.Equals(Path.GetFullPath(current),Path.GetFullPath(uninstaller),StringComparison.OrdinalIgnoreCase))File.Copy(current,uninstaller,true);
        var register=Process.Start(new ProcessStartInfo(Path.Combine(Home,"FlameCursor.exe"),"--register"){UseShellExecute=false,CreateNoWindow=true});
        register.WaitForExit();if(register.ExitCode!=0)throw new IOException("Could not register Cursor Settings.");
    }
    static void Remove(int parent) {
        if(parent>0)try{Process.GetProcessById(parent).WaitForExit(10000);}catch(ArgumentException){}
        string full=Path.GetFullPath(Home);
        string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlameCursor");
        if(!string.Equals(full,expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Unexpected installation folder.");
        string exe=Path.Combine(full,"FlameCursor.exe");
        if(File.Exists(exe)) {
            var restore=Process.Start(new ProcessStartInfo(exe,"--restore-silent"){UseShellExecute=false,CreateNoWindow=true});restore.WaitForExit();
            if(restore.ExitCode!=0)throw new IOException("Pointer restoration failed; installation was preserved.");
        }
        using(var menu=Registry.CurrentUser.OpenSubKey(@"Software\Classes\DesktopBackground\shell\FlameCursorSettings\command")) {
            if(menu!=null&&(string)menu.GetValue("")=="\""+exe+"\"")Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\DesktopBackground\shell\FlameCursorSettings",false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FlameCursor",false);
        string link=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Cursor Settings.lnk");if(File.Exists(link))File.Delete(link);
        foreach(string name in new[]{"FlameCursor.exe","Uninstall.exe","READ ME.txt","settings.xml","settings.xml.bak","settings.xml.tmp","last-error.txt","physics-error.txt"})Delete(Path.Combine(full,name));
        foreach(string file in Directory.GetFiles(full,"cursor-*.ani"))Delete(file);
        if(Directory.GetFileSystemEntries(full).Length==0)Directory.Delete(full,false);
        // This isolated remover is in the user's Temp folder, never inside the installation.
    }
    static void Delete(string file){for(int i=0;i<30;i++){try{if(File.Exists(file))File.Delete(file);return;}catch(IOException){Thread.Sleep(100);}}throw new IOException("File still in use: "+file);}
}
