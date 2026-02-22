using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Code.EditorControls
{
    public static class FileOpener
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        public static string OpenWavFile()
        {
            string result = null;

            var thread = new Thread(() =>
            {
                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(ofn);
                ofn.lpstrFile = new string('\0', 256);
                ofn.nMaxFile = ofn.lpstrFile.Length;
                ofn.lpstrFilter = "WAV Dateien\0*.wav\0Alle Dateien\0*.*\0";
                ofn.nFilterIndex = 1;
                ofn.lpstrTitle = "Wähle eine WAV Datei";
                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800;

                if (GetOpenFileName(ref ofn))
                    result = ofn.lpstrFile.TrimEnd('\0');
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return result;
        }
    }
}