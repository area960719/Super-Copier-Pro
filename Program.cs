using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuperCopierPro
{
    enum Estado { Pendiente, Copiando, Completado, Omitido, Error, Cancelado }
    enum Colision { Preguntar, Sobrescribir, Omitir, Renombrar, SoloSiEsMasNuevo }

    class Item
    {
        public string Origen = "";
        public string Rel = "";
        public long Tam;
        public long Copiado;
        public Estado Estado = Estado.Pendiente;
        public string Msg = "";
        public ListViewItem Fila;
        public int Prioridad = 1;
    }

    static class LogCopias
    {
        static readonly object _lock = new object();

        public static void Escribir(string mensaje)
        {
            try
            {
                string rutaLog = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SuperCopierPro.log"
                );
                string linea = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + mensaje + Environment.NewLine;
                lock (_lock)
                {
                    File.AppendAllText(rutaLog, linea);
                }
            }
            catch { }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Principal());
        }
    }

    class Principal : Form
    {
        const int TB = 1024 * 1024;

        readonly List<Item> items = new List<Item>();
        readonly HashSet<string> rutasEnLista = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> carpetasRaiz = new List<string>();
        readonly HashSet<string> dirsMovidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        readonly ListView lista = new ListView();
        readonly TextBox txtDestino = new TextBox { Width = 430 };
        readonly RadioButton rbCopiar = new RadioButton { Text = "Copiar", Checked = true, AutoSize = true };
        readonly RadioButton rbMover = new RadioButton { Text = "Mover", AutoSize = true };
        readonly CheckBox chkSuperMover = new CheckBox { Text = "Mover con verificación", AutoSize = true };
        readonly ComboBox cmbColision = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        readonly NumericUpDown numLimite = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 0, Width = 70 };
        readonly CheckBox chkVerificar = new CheckBox { Text = "Verificar copia (SHA-256)", AutoSize = true };
        readonly NumericUpDown numReintentos = new NumericUpDown { Minimum = 0, Maximum = 10, Value = 3, Width = 50 };
        readonly NumericUpDown numDelayReint = new NumericUpDown { Minimum = 0, Maximum = 60, Value = 1, Width = 50 };
        readonly Button btnAgregarArch = new Button { Text = "Agregar archivos", AutoSize = true };
        readonly Button btnAgregarCarp = new Button { Text = "Agregar carpeta", AutoSize = true };
        readonly Button btnQuitar = new Button { Text = "Quitar seleccionados", AutoSize = true };
        readonly Button btnLimpiar = new Button { Text = "Limpiar lista", AutoSize = true };
        readonly Button btnIniciar = new Button { Text = "▶ Iniciar", AutoSize = true };
        readonly Button btnPausa = new Button { Text = "⏸ Pausar", AutoSize = true, Enabled = false };
        readonly Button btnCancelar = new Button { Text = "⏹ Cancelar", AutoSize = true, Enabled = false };
        readonly ProgressBar barraTotal = new ProgressBar { Dock = DockStyle.Top, Height = 22, Maximum = 1000 };
        readonly ProgressBar barraArchivo = new ProgressBar { Dock = DockStyle.Top, Height = 14, Maximum = 1000 };
        readonly Label lblEstado = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "Listo. Arrastra archivos o carpetas a la lista." };
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 500 };

        readonly Label lblArchivoActual = new Label { AutoSize = true };
        readonly Label lblTamActual = new Label { AutoSize = true };
        readonly Label lblVelInst = new Label { AutoSize = true };
        readonly Label lblVelMed = new Label { AutoSize = true };

        readonly ManualResetEventSlim pausa = new ManualResetEventSlim(true);
        CancellationTokenSource cts;
        volatile bool ejecutando;
        volatile bool cerrarAlTerminar;
        long totalBytes, hechoBytes, ultimoHecho, limiteBps;
        DateTime ultimoTick;
        double velMedia;
        volatile Item actual;

        public Principal()
        {
            Text = "SuperCopier Pro";
            Size = new Size(1000, 640);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen;

            // Icono de la ventana (toma el del .exe incrustado)
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            lista.Dock = DockStyle.Fill;
            lista.View = View.Details;
            lista.FullRowSelect = true;
            lista.GridLines = true;
            lista.AllowDrop = true;
            lista.Columns.Add("Archivo", 300);
            lista.Columns.Add("Carpeta origen", 250);
            lista.Columns.Add("Tamaño", 90);
            lista.Columns.Add("Progreso", 70);
            lista.Columns.Add("Estado", 220);
            lista.Columns.Add("Prioridad", 70);
            lista.DragEnter += (s, e) =>
            {
                if (!ejecutando && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            lista.DragDrop += (s, e) =>
            {
                if (ejecutando || e.Data == null) return;
                var soltados = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (soltados != null)
                    EnLote(() =>
                    {
                        foreach (var r in soltados)
                        {
                            if (Directory.Exists(r)) AgregarCarpeta(r);
                            else if (File.Exists(r)) AgregarArchivo(r, Path.GetFileName(r));
                        }
                    });
            };

            cmbColision.Items.AddRange(new object[]
            {
                "Preguntar", "Sobrescribir", "Omitir", "Renombrar: nombre (1)", "Sobrescribir solo si es más nuevo"
            });
            cmbColision.SelectedIndex = 0;
            numLimite.ValueChanged += (s, e) =>
                Interlocked.Exchange(ref limiteBps, (long)numLimite.Value * 1024 * 1024);

            var btnDestino = new Button { Text = "…", AutoSize = true };
            btnDestino.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Carpeta de destino" })
                {
                    if (d.ShowDialog() == DialogResult.OK) txtDestino.Text = d.SelectedPath;
                }
            };

            btnAgregarArch.Click += (s, e) =>
            {
                using (var d = new OpenFileDialog { Multiselect = true, Title = "Selecciona archivos" })
                {
                    if (d.ShowDialog() == DialogResult.OK)
                    {
                        var nombres = d.FileNames;
                        EnLote(() => { foreach (var f in nombres) AgregarArchivo(f, Path.GetFileName(f)); });
                    }
                }
            };
            btnAgregarCarp.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Carpeta a copiar" })
                {
                    if (d.ShowDialog() == DialogResult.OK)
                    {
                        var ruta = d.SelectedPath;
                        EnLote(() => AgregarCarpeta(ruta));
                    }
                }
            };
            btnQuitar.Click += (s, e) =>
            {
                lista.BeginUpdate();
                foreach (var lvi in lista.SelectedItems.Cast<ListViewItem>().ToList())
                {
                    var it = lvi.Tag as Item;
                    if (it != null) { items.Remove(it); rutasEnLista.Remove(it.Origen); }
                    lista.Items.Remove(lvi);
                }
                lista.EndUpdate();
            };
            btnLimpiar.Click += (s, e) =>
            {
                items.Clear(); rutasEnLista.Clear(); carpetasRaiz.Clear(); lista.Items.Clear();
                barraTotal.Value = 0; barraArchivo.Value = 0;
                lblEstado.Text = "Lista vacía.";
            };
            btnIniciar.Click += Iniciar_Click;
            btnPausa.Click += (s, e) =>
            {
                if (pausa.IsSet) { pausa.Reset(); btnPausa.Text = "▶ Reanudar"; }
                else { pausa.Set(); btnPausa.Text = "⏸ Pausar"; }
            };
            btnCancelar.Click += (s, e) => { if (cts != null) cts.Cancel(); pausa.Set(); };
            timer.Tick += Tick;

            var sup = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Padding = new Padding(8)
            };
            var f1 = new FlowLayoutPanel { AutoSize = true };
            f1.Controls.AddRange(new Control[] { Lbl("Destino:"), txtDestino, btnDestino, rbCopiar, rbMover, chkSuperMover });
            var f2 = new FlowLayoutPanel { AutoSize = true };
            f2.Controls.AddRange(new Control[]
            {
                Lbl("Si el archivo existe:"), cmbColision,
                Lbl("Límite MB/s (0 = sin límite):"), numLimite, chkVerificar,
                Lbl("Reintentos:"), numReintentos,
                Lbl("Delay s:"), numDelayReint
            });
            var f3 = new FlowLayoutPanel { AutoSize = true };
            f3.Controls.AddRange(new Control[]
            {
                btnAgregarArch, btnAgregarCarp, btnQuitar, btnLimpiar,
                new Label { Width = 30 }, btnIniciar, btnPausa, btnCancelar
            });
            sup.Controls.AddRange(new Control[] { f1, f2, f3 });

            var inf = new Panel { Dock = DockStyle.Bottom, Height = 120, Padding = new Padding(8) };
            inf.Controls.Add(lblEstado);
            inf.Controls.Add(barraArchivo);
            inf.Controls.Add(barraTotal);

            var panelDetalles = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight
            };
            panelDetalles.Controls.AddRange(new Control[]
            {
                Lbl("Actual:"), lblArchivoActual,
                Lbl("Tamaño:"), lblTamActual,
                Lbl("Vel inst:"), lblVelInst,
                Lbl("Vel med:"), lblVelMed
            });
            inf.Controls.Add(panelDetalles);

            Controls.Add(lista);
            Controls.Add(inf);
            Controls.Add(sup);

            FormClosing += (s, e) =>
            {
                if (!ejecutando) return;
                if (MessageBox.Show("Hay una copia en curso. ¿Cancelar y salir?", "SuperCopier Pro",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    cerrarAlTerminar = true;
                    if (cts != null) cts.Cancel();
                    pausa.Set();
                }
                e.Cancel = true;
            };
        }

        static Label Lbl(string t) { return new Label { Text = t, AutoSize = true, Margin = new Padding(8, 7, 3, 0) }; }

        void EnLote(Action accion)
        {
            lista.BeginUpdate();
            try { accion(); }
            finally { lista.EndUpdate(); }
            lblEstado.Text = items.Count + " archivos en la cola (" + Fmt(items.Sum(i => i.Tam)) + ").";
        }

        void AgregarArchivo(string ruta, string rel)
        {
            if (ejecutando || !rutasEnLista.Add(ruta)) return;
            long tam;
            try { tam = new FileInfo(ruta).Length; }
            catch { rutasEnLista.Remove(ruta); return; }
            var it = new Item { Origen = ruta, Rel = rel, Tam = tam, Prioridad = 1 };
            var lvi = new ListViewItem(new[]
            {
                rel, Path.GetDirectoryName(ruta) ?? "", Fmt(tam), "0%", "Pendiente", "Normal"
            }) { Tag = it };
            it.Fila = lvi;
            items.Add(it);
            lista.Items.Add(lvi);
        }

        void AgregarCarpeta(string dir)
        {
            if (ejecutando) return;
            dir = dir.TrimEnd(Path.DirectorySeparatorChar);
            if (!carpetasRaiz.Contains(dir, StringComparer.OrdinalIgnoreCase)) carpetasRaiz.Add(dir);
            string nombre = Path.GetFileName(dir);
            foreach (var f in EnumerateFilesSafe(dir))
                AgregarArchivo(f, Path.Combine(nombre, GetRelativePath(dir, f)));
        }

        static IEnumerable<string> EnumerateFilesSafe(string dir)
        {
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                string[] files = null;
                try { files = Directory.GetFiles(d); } catch { }
                if (files != null) foreach (var f in files) yield return f;
                string[] subs = null;
                try { subs = Directory.GetDirectories(d); } catch { }
                if (subs != null) foreach (var s in subs) stack.Push(s);
            }
        }

        static string GetRelativePath(string relativeTo, string path)
        {
            var fromUri = new Uri(AppendDirSep(relativeTo));
            var toUri = new Uri(path);
            var relUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relUri.ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        static string AppendDirSep(string p)
        {
            return p.EndsWith(Path.DirectorySeparatorChar.ToString()) ? p : p + Path.DirectorySeparatorChar;
        }

        async void Iniciar_Click(object s, EventArgs e)
        {
            if (ejecutando) return;
            if (items.Count == 0) { MessageBox.Show("Agrega archivos o carpetas primero."); return; }
            string destRoot = txtDestino.Text.Trim();
            if (destRoot == "") { MessageBox.Show("Elige una carpeta de destino."); return; }
            try { Directory.CreateDirectory(destRoot); }
            catch (Exception ex) { MessageBox.Show("Destino inválido: " + ex.Message); return; }

            bool mover = rbMover.Checked;
            bool verificar = chkVerificar.Checked || (mover && chkSuperMover.Checked);
            var col = (Colision)cmbColision.SelectedIndex;
            Interlocked.Exchange(ref limiteBps, (long)numLimite.Value * 1024 * 1024);
            int maxReintentos = (int)numReintentos.Value;
            int delayReintSeg = (int)numDelayReint.Value;

            lista.BeginUpdate();
            foreach (var it in items)
            {
                it.Copiado = 0; it.Estado = Estado.Pendiente; it.Msg = "";
                RefrescarFila(it);
            }
            lista.EndUpdate();
            totalBytes = items.Sum(i => i.Tam);
            Interlocked.Exchange(ref hechoBytes, 0);
            ultimoHecho = 0; velMedia = 0; ultimoTick = DateTime.UtcNow;
            dirsMovidos.Clear();

            cts = new CancellationTokenSource();
            pausa.Set();
            btnPausa.Text = "⏸ Pausar";
            EstadoUI(true);
            timer.Start();

            var copia = items
                .OrderBy(i => i.Prioridad)
                .ThenBy(i => i.Rel)
                .ToList();

            try { await Task.Run(() => Procesar(copia, destRoot, mover, col, verificar, maxReintentos, delayReintSeg, cts.Token)); }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }

            timer.Stop();
            actual = null;
            if (mover) LimpiarOrigenes(dirsMovidos);
            lista.BeginUpdate();
            foreach (var it in copia) RefrescarFila(it);
            lista.EndUpdate();

            int ok = copia.Count(i => i.Estado == Estado.Completado);
            int om = copia.Count(i => i.Estado == Estado.Omitido);
            int er = copia.Count(i => i.Estado == Estado.Error);
            int ca = copia.Count(i => i.Estado == Estado.Cancelado);
            barraArchivo.Value = 0;
            barraTotal.Value = ca == 0 ? 1000 : barraTotal.Value;
            lblEstado.Text = "Terminado: " + ok + " completados, " + om + " omitidos, " + er + " con error, " + ca + " cancelados.";
            EstadoUI(false);

            if (cerrarAlTerminar) { Close(); return; }
            System.Media.SystemSounds.Asterisk.Play();
        }

        void EstadoUI(bool corriendo)
        {
            ejecutando = corriendo;
            btnIniciar.Enabled = btnAgregarArch.Enabled = btnAgregarCarp.Enabled =
                btnQuitar.Enabled = btnLimpiar.Enabled = !corriendo;
            btnPausa.Enabled = btnCancelar.Enabled = corriendo;
            rbCopiar.Enabled = rbMover.Enabled = txtDestino.Enabled = cmbColision.Enabled =
                chkVerificar.Enabled = chkSuperMover.Enabled = !corriendo;
        }

        async Task Procesar(List<Item> cola, string destRoot, bool mover, Colision col,
                            bool verificar, int maxReintentos, int delayReintSeg, CancellationToken ct)
        {
            try
            {
                foreach (var it in cola)
                {
                    ct.ThrowIfCancellationRequested();
                    pausa.Wait(ct);
                    actual = it;
                    string dest = Path.Combine(destRoot, it.Rel);

                    if (Path.GetFullPath(dest).Equals(Path.GetFullPath(it.Origen), StringComparison.OrdinalIgnoreCase))
                    {
                        Omitir(it, "Origen y destino son iguales");
                        continue;
                    }

                    if (File.Exists(dest))
                    {
                        var accion = col == Colision.Preguntar ? Preguntar(dest, it.Origen) : col;
                        if (accion == Colision.Omitir) { Omitir(it, "Ya existe"); continue; }
                        if (accion == Colision.SoloSiEsMasNuevo &&
                            File.GetLastWriteTimeUtc(it.Origen) <= File.GetLastWriteTimeUtc(dest))
                        { Omitir(it, "El destino es igual o más nuevo"); continue; }
                        if (accion == Colision.Renombrar) dest = NombreLibre(dest);
                    }

                    it.Estado = Estado.Copiando;
                    Actualizar(it);

                    if (mover && !verificar && MismoVolumen(it.Origen, dest))
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest));
                            if (File.Exists(dest))
                            {
                                File.SetAttributes(dest, FileAttributes.Normal);
                                File.Delete(dest);
                            }
                            File.Move(it.Origen, dest);
                            it.Copiado = it.Tam;
                            Interlocked.Add(ref hechoBytes, it.Tam);
                            it.Msg = "movido (mismo disco)";
                            it.Estado = Estado.Completado;
                            RecordarOrigen(it);
                            LogCopias.Escribir("MOVIDO (mismo disco) | " + it.Origen + " -> " + dest + " | " + Fmt(it.Tam));
                        }
                        catch (Exception ex)
                        {
                            it.Msg = ex.Message;
                            it.Estado = Estado.Error;
                            Interlocked.Add(ref hechoBytes, it.Tam);
                            LogCopias.Escribir("ERROR AL MOVER (mismo disco) | " + it.Origen + " -> " + dest + " | " + ex.Message);
                        }
                        Actualizar(it);
                        continue;
                    }

                    bool ok = false;
                    for (int intento = 1; intento <= maxReintentos && !ok; intento++)
                    {
                        try
                        {
                            await CopiarArchivo(it, dest, verificar, ct);
                            ok = true;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (IOException ex)
                        {
                            Interlocked.Add(ref hechoBytes, -it.Copiado);
                            it.Copiado = 0;
                            it.Msg = ex.Message + " (intento " + intento + "/" + maxReintentos + ")";
                            Actualizar(it);
                            LogCopias.Escribir("REINTENTO " + intento + "/" + maxReintentos + " | " + it.Origen + " -> " + dest + " | " + ex.Message);
                            if (intento < maxReintentos && delayReintSeg > 0)
                                await Task.Delay(delayReintSeg * 1000, ct);
                        }
                        catch (Exception ex)
                        {
                            it.Msg = ex.Message + " (sin reintento)";
                            Actualizar(it);
                            LogCopias.Escribir("ERROR NO REINTENTABLE | " + it.Origen + " -> " + dest + " | " + ex.Message);
                            break;
                        }
                    }

                    if (ok)
                    {
                        it.Msg = "";
                        if (mover)
                        {
                            try { File.Delete(it.Origen); RecordarOrigen(it); }
                            catch (Exception ex) { it.Msg = "Copiado, pero no se pudo borrar el origen: " + ex.Message; }
                        }
                        it.Estado = Estado.Completado;
                        LogCopias.Escribir("OK | " + it.Origen + " -> " + dest + " | " + Fmt(it.Tam));
                    }
                    else
                    {
                        it.Estado = Estado.Error;
                        Interlocked.Add(ref hechoBytes, it.Tam);
                        LogCopias.Escribir("ERROR FINAL | " + it.Origen + " -> " + dest + " | " + it.Msg);
                    }
                    Actualizar(it);
                }
            }
            catch (OperationCanceledException)
            {
                foreach (var it in cola.Where(i => i.Estado == Estado.Pendiente || i.Estado == Estado.Copiando))
                {
                    it.Estado = Estado.Cancelado;
                    Actualizar(it);
                    LogCopias.Escribir("CANCELADO | " + it.Origen);
                }
            }
        }

        void Omitir(Item it, string motivo)
        {
            it.Estado = Estado.Omitido;
            it.Msg = motivo;
            Interlocked.Add(ref hechoBytes, it.Tam);
            LogCopias.Escribir("OMITIDO | " + it.Origen + " | " + motivo);
            Actualizar(it);
        }

        Colision Preguntar(string dest, string origen)
        {
            Colision r = Colision.Omitir;
            Invoke(new Action(() =>
            {
                var infoDest = new FileInfo(dest);
                var infoOrig = new FileInfo(origen);
                string detalles = "Origen:  " + Fmt(infoOrig.Length) + " | " + infoOrig.LastWriteTime + "\n" +
                                  "Destino: " + Fmt(infoDest.Length) + " | " + infoDest.LastWriteTime + "\n";
                var res = MessageBox.Show(this,
                    "El archivo ya existe:\n" + dest + "\n\n" + detalles + "\n" +
                    "Sí = Sobrescribir\nNo = Omitir\nCancelar = Renombrar",
                    "Archivo existente", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                r = res == DialogResult.Yes ? Colision.Sobrescribir
                  : res == DialogResult.No ? Colision.Omitir : Colision.Renombrar;
            }));
            return r;
        }

        async Task CopiarArchivo(Item it, string dest, bool verificar, CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string tmp = dest + ".sc_tmp";

            int tamBuffer;
            if (it.Tam < 50L * 1024 * 1024) tamBuffer = 512 * 1024;
            else if (it.Tam < 500L * 1024 * 1024) tamBuffer = 1024 * 1024;
            else tamBuffer = 4 * 1024 * 1024;

            var buffer = new byte[tamBuffer];
            var dummy = new byte[tamBuffer];

            SHA256Managed hasher = verificar ? new SHA256Managed() : null;

            try
            {
                var optL = FileOptions.SequentialScan | FileOptions.Asynchronous;
                var optE = FileOptions.Asynchronous;

                using (var src = new FileStream(it.Origen, FileMode.Open, FileAccess.Read,
                                                FileShare.ReadWrite, 4096, optL))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write,
                                                FileShare.None, 4096, optE))
                {
                    if (it.Tam > 0) { try { dst.SetLength(it.Tam); } catch { } }

                    var sw = Stopwatch.StartNew();
                    long enVentana = 0;
                    long limPrev = Interlocked.Read(ref limiteBps);
                    int n;
                    while ((n = await src.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        if (!pausa.IsSet)
                        {
                            pausa.Wait(ct);
                            sw.Restart(); enVentana = 0;
                        }
                        await dst.WriteAsync(buffer, 0, n, ct);
                        if (hasher != null) hasher.TransformBlock(buffer, 0, n, dummy, 0);
                        it.Copiado += n;
                        Interlocked.Add(ref hechoBytes, n);
                        enVentana += n;

                        long lim = Interlocked.Read(ref limiteBps);
                        if (lim != limPrev)
                        {
                            limPrev = lim;
                            sw.Restart(); enVentana = 0;
                        }
                        else if (lim > 0)
                        {
                            long esperadoMs = enVentana * 1000 / lim;
                            long realMs = sw.ElapsedMilliseconds;
                            if (esperadoMs > realMs) await Task.Delay((int)(esperadoMs - realMs), ct);
                        }
                    }
                }

                if (hasher != null)
                {
                    it.Msg = "Verificando...";
                    hasher.TransformFinalBlock(new byte[0], 0, 0);
                    byte[] h1 = hasher.Hash;
                    byte[] h2 = await Task.Run(() =>
                    {
                        using (var b = File.OpenRead(tmp))
                        using (var sha = SHA256.Create())
                            return sha.ComputeHash(b);
                    }, ct);
                    if (!BytesEqual(h1, h2)) throw new IOException("La verificación falló (hash distinto)");
                    it.Msg = "";
                }

                if (File.Exists(dest))
                {
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { }
                    try { File.Delete(dest); } catch { }
                }
                File.Move(tmp, dest);
                File.SetLastWriteTimeUtc(dest, File.GetLastWriteTimeUtc(it.Origen));
                try
                {
                    var atr = File.GetAttributes(it.Origen) &
                              (FileAttributes.ReadOnly | FileAttributes.Hidden |
                               FileAttributes.System | FileAttributes.Archive);
                    File.SetAttributes(dest, atr == 0 ? FileAttributes.Normal : atr);
                }
                catch { }
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
            finally { if (hasher != null) hasher.Dispose(); }
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static bool MismoVolumen(string a, string b)
        {
            return string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }

        static string NombreLibre(string ruta)
        {
            string dir = Path.GetDirectoryName(ruta);
            string nom = Path.GetFileNameWithoutExtension(ruta);
            string ext = Path.GetExtension(ruta);
            int n = 1;
            string r;
            do { r = Path.Combine(dir, nom + " (" + (n++) + ")" + ext); } while (File.Exists(r));
            return r;
        }

        void RecordarOrigen(Item it)
        {
            var d = Path.GetDirectoryName(it.Origen);
            if (d != null) dirsMovidos.Add(d);
        }

        bool DentroDeRaiz(string d)
        {
            return carpetasRaiz.Any(r =>
                d.Equals(r, StringComparison.OrdinalIgnoreCase) ||
                d.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        void LimpiarOrigenes(IEnumerable<string> dirs)
        {
            foreach (var d0 in dirs.ToList())
            {
                string d = d0;
                while (d != null && DentroDeRaiz(d))
                {
                    try
                    {
                        if (Directory.EnumerateFileSystemEntries(d).Any()) break;
                        Directory.Delete(d);
                    }
                    catch { break; }
                    d = Path.GetDirectoryName(d);
                }
            }
        }

        void Actualizar(Item it)
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(() => RefrescarFila(it)));
        }

        void RefrescarFila(Item it)
        {
            var f = it.Fila;
            if (f == null) return;
            f.SubItems[3].Text = it.Estado == Estado.Completado ? "100%"
                : it.Tam > 0 ? (100.0 * it.Copiado / it.Tam).ToString("0") + "%" : "0%";
            f.SubItems[4].Text = it.Estado + (it.Msg != "" ? " – " + it.Msg : "");
            string prio;
            switch (it.Prioridad)
            {
                case 0: prio = "Alta"; break;
                case 2: prio = "Baja"; break;
                default: prio = "Normal"; break;
            }
            f.SubItems[5].Text = prio;
            switch (it.Estado)
            {
                case Estado.Completado: f.ForeColor = Color.ForestGreen; break;
                case Estado.Error: f.ForeColor = Color.Firebrick; break;
                case Estado.Omitido: f.ForeColor = Color.Gray; break;
                case Estado.Cancelado: f.ForeColor = Color.DarkOrange; break;
                default: f.ForeColor = SystemColors.WindowText; break;
            }
        }

        void Tick(object s, EventArgs e)
        {
            var ahora = DateTime.UtcNow;
            double dt = (ahora - ultimoTick).TotalSeconds;
            if (dt <= 0) return;
            long h = Interlocked.Read(ref hechoBytes);

            double v = pausa.IsSet ? (h - ultimoHecho) / dt : 0;
            velMedia = velMedia == 0 ? v : velMedia * 0.7 + v * 0.3;
            if (!pausa.IsSet) velMedia = 0;
            ultimoHecho = h; ultimoTick = ahora;

            barraTotal.Value = totalBytes > 0 ? Clamp((int)(h * 1000 / totalBytes), 0, 1000) : 0;
            var a = actual;
            if (a != null)
            {
                barraArchivo.Value = a.Tam > 0 ? Clamp((int)(a.Copiado * 1000 / a.Tam), 0, 1000) : 0;
                RefrescarFila(a);
                lblArchivoActual.Text = Path.GetFileName(a.Origen);
                lblTamActual.Text = Fmt(a.Tam);
            }

            lblVelInst.Text = Fmt((long)v) + "/s";
            lblVelMed.Text = Fmt((long)velMedia) + "/s";

            string eta = "--:--:--";
            if (velMedia > 1)
            {
                var t = TimeSpan.FromSeconds(Math.Max(0, (totalBytes - h) / velMedia));
                eta = ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            }
            int hechos = items.Count(i => i.Estado == Estado.Completado || i.Estado == Estado.Omitido || i.Estado == Estado.Error);
            string estadoTxt = pausa.IsSet ? "Copiando" : "EN PAUSA";
            lblEstado.Text = estadoTxt + ": " + Fmt(h) + " / " + Fmt(totalBytes) + "  |  " + Fmt((long)velMedia) + "/s  |  " +
                             "Restante: " + eta + "  |  Archivos: " + hechos + "/" + items.Count +
                             (a != null ? "  |  Actual: " + Path.GetFileName(a.Origen) : "");
        }

        static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        static string Fmt(long b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = b;
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return v.ToString("0.##") + " " + u[i];
        }
    }
}
