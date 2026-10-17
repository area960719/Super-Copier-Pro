using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.SharpDropHandler;

namespace SuperCopierShellExt
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFilesAndFolders)]
    public class SuperCopierDropHandler : SharpDropHandler
    {
        private const string RutaEjecutable = @"C:\Apps\SuperCopier\Super-Copier-Pro.exe";

        // ✅ Se usa System.Windows.Forms.DragEventArgs
        protected override void DragEnter(DragEventArgs dragEventArgs)
        {
            // Se usa System.Windows.Forms.DragDropEffects.Copy
            dragEventArgs.Effect = DragDropEffects.Copy;
        }

        // ✅ Se usa System.Windows.Forms.DragEventArgs
        protected override void Drop(DragEventArgs dragEventArgs)
        {
            // ✅ Se usa el nombre completo para evitar ambigüedad
            var dataObject = dragEventArgs.Data as System.Windows.Forms.IDataObject;
            if (dataObject == null || !dataObject.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = dataObject.GetData(DataFormats.FileDrop) as string[];
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
    }
}
