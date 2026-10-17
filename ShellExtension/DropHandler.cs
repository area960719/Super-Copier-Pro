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

// Estas dos líneas son obligatorias para que Windows acepte la DLL
[assembly: ComVisible(true)]
[assembly: Guid("F1A2B3C4-D5E6-7890-1234-567890ABCDEF")]

namespace SuperCopierShellExt
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFilesAndFolders)]
    public class SuperCopierDropHandler : SharpDropHandler
    {
        // La ruta exacta donde estará tu programa principal
        private const string RutaEjecutable = @"C:\Apps\SuperCopier\Super-Copier-Pro.exe";

        protected override void DragEnter(SharpShell.Interop.DragEventArgs dragEventArgs)
        {
            // 1 significa DROPEFFECT_COPY (muestra el cursor de "Copiar")
            dragEventArgs.dwEffect = 1; 
        }

        protected override void Drop(SharpShell.Interop.DragEventArgs dragEventArgs)
        {
            // Obtenemos las rutas de los archivos arrastrados
            var paths = GetPaths(dragEventArgs.pDataObj);
            if (paths == null || paths.Length == 0) return;

            if (!File.Exists(RutaEjecutable)) return;

            // Preparamos los argumentos para pasárselos a SuperCopier
            var args = new StringBuilder();
            foreach (var ruta in paths)
            {
                if (args.Length > 0) args.Append(' ');
                args.Append('"').Append(ruta).Append('"');
            }

            try
            {
                // Lanzamos tu SuperCopier con los archivos
                Process.Start(new ProcessStartInfo
                {
                    FileName = RutaEjecutable,
                    Arguments = args.ToString(),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ============ Código nativo para extraer las rutas ============
        private string[] GetPaths(System.Runtime.InteropServices.ComTypes.IDataObject dataObject)
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
