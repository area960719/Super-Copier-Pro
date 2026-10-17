using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SharpShell.Attributes;
using SharpShell.SharpDropHandler;

namespace SuperCopierShellExt
{
    // Se registra para que actúe sobre todos los archivos y carpetas
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFilesAndFolders)]
    public class SuperCopierDropHandler : SharpDropHandler
    {
        // Ruta al ejecutable de tu SuperCopier.
        // CÁMBIALA si decides ponerlo en otra carpeta en tu PC.
        private const string RutaEjecutable = @"C:\Apps\SuperCopier\Super-Copier-Pro.exe";

        protected override void DragEnter(DragEventArgs dragEventArgs)
        {
            // Mostramos el efecto de "Copiar" al arrastrar
            dragEventArgs.Effect = DragDropEffects.Copy;
        }

        protected override void Drop(DragEventArgs dragEventArgs)
        {
            // Obtenemos las rutas de los archivos arrastrados
            var rutas = (string[])dragEventArgs.Data.GetData(DataFormats.FileDrop);
            if (rutas == null || rutas.Length == 0) return;

            // Verificamos que el ejecutable exista
            if (!File.Exists(RutaEjecutable)) return;

            // Construimos los argumentos, entrecomillando cada ruta
            var args = new StringBuilder();
            foreach (var ruta in rutas)
            {
                if (args.Length > 0) args.Append(' ');
                args.Append('"').Append(ruta).Append('"');
            }

            // Lanzamos SuperCopier con los archivos como argumentos
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = RutaEjecutable,
                    Arguments = args.ToString(),
                    UseShellExecute = true
                });
            }
            catch { /* Si algo falla, no hacemos nada para no romper el Explorador */ }
        }
    }
}
