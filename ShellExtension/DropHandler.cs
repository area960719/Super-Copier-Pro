using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.Interop;
using SharpShell.SharpDropHandler;

namespace SuperCopierShellExt
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFilesAndFolders)]
    public class SuperCopierDropHandler : SharpDropHandler
    {
        private const string RutaEjecutable = @"C:\Apps\SuperCopier\Super-Copier-Pro.exe";

        protected override void DragEnter(DragEventArgs dragEventArgs)
        {
            // 1 es DROPEFFECT_COPY (Copiar)
            dragEventArgs.dwEffect = 1; 
        }

        protected override void Drop(DragEventArgs dragEventArgs)
        {
            // Obtenemos las rutas usando código nativo de Windows
            var paths = GetPaths(dragEventArgs.pDataObj);
            if (paths == null || paths.Length == 0) return;

            if (!File.Exists(RutaEjecutable)) return;

            var args = new StringBuilder();
            foreach (var ruta in paths)
            {
                if (args.Length > 0) args.Append(' ');
                args.Append('"').Append(ruta).Append('"');
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = RutaEjecutable,
                    Arguments = args.ToString(),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ============ Extracción de rutas de SharpShell ============
        private string[] GetPaths(IDataObject dataObject)
        {
            if (dataObject == null) return null;
            var format = new FORMATETC
            {
                cfFormat = (short)DataFormats.GetFormat(DataFormats.FileDrop).Id,
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = TYMED.TYMED_HGLOBAL
            };
            STGMEDIUM medium;
            dataObject.GetData(ref format, out medium);

            try
            {
                var hDrop = medium.unionmember;
                if (hDrop == IntPtr.Zero) return null;

                uint count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                var paths = new string[count];
                for (uint i = 0; i < count; i++)
                {
                    uint size = DragQueryFile(hDrop, i, null, 0) + 1;
                    var sb = new StringBuilder((int)size);
                    DragQueryFile(hDrop, i, sb, size);
                    paths[i] = sb.ToString();
                }
                return paths;
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, uint cch);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
    }
}
