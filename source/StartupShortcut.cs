using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

public static class StartupShortcut {
    public static void Set(string path, string executable, bool enabled) {
        if (!enabled) { if (File.Exists(path)) File.Delete(path); return; }
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(shellType);
        object shortcut = null;
        try {
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            var t = shortcut.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { executable });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(executable) });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Codex quota widget" });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        } finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
}
