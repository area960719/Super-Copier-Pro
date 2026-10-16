# 🚀 SuperCopier Pro

Un copiador y movedor de archivos para Windows, rápido, ligero y con verificación SHA-256 integrada. Diseñado para mover grandes cantidades de datos entre discos con control total sobre la velocidad, colisiones y errores.

Funciona en cualquier Windows moderno **sin necesidad de instalar nada adicional**, gracias a que está compilado para **.NET Framework 4.5**.

---

## ✨ Características principales

### 📂 Copia y movimiento de archivos
- **Modo Copiar**: duplica archivos y carpetas al destino sin tocar el origen.
- **Modo Mover**: mueve archivos al destino y elimina el origen.
- **Mover inteligente**: si origen y destino están en el mismo disco, usa renombrado instantáneo (sin recopiar datos).
- **Mover con verificación**: copia, verifica y solo entonces borra el origen.
- **Limpieza automática**: al mover una carpeta, elimina los directorios vacíos que quedaron atrás.

### ✅ Verificación SHA-256
- Opción de **verificar cada copia con SHA-256** para asegurar que el archivo llegó íntegro.
- El hash se calcula **mientras se copia**, sin leer el origen dos veces.
- Si la verificación falla, el archivo se marca como erróneo y no se sobreescribe el destino.

### ⚡ Control de velocidad
- **Límite de velocidad configurable** en MB/s (0 = sin límite).
- Cambio del límite **en caliente**, incluso con la copia en curso.
- Estadísticas en vivo: velocidad instantánea, velocidad media, tiempo restante estimado.

### 🧠 Manejo de colisiones
Cuando el archivo destino ya existe, puedes elegir:
- **Preguntar** caso por caso.
- **Sobrescribir** siempre.
- **Omitir** siempre.
- **Renombrar**: añade `nombre (1)`, `nombre (2)`, etc.
- **Sobrescribir solo si es más nuevo** (compara fechas de modificación).

### 🔁 Reintentos automáticos
- Configura el **número de reintentos** para errores de E/S.
- Configura el **retardo entre reintentos** en segundos.
- Los errores no recuperables se detectan y no se reintentan sin sentido.

### 🎨 Interfaz
- **Tema claro y oscuro** con un solo botón (🌙 / ☀).
- **Barras de progreso con color según el estado**:
  - 🟢 Verde mientras copia.
  - 🟠 Naranja al pausar.
  - 🔴 Rojo si hay errores.
  - 🔵 Azul cuando está inactivo.
- **Emojis** en botones y columnas.
- **Fuente Segoe UI** para un aspecto limpio y moderno.
- **Doble buffer** en la lista: sin parpadeos al refrescar.
- **Arrastrar y soltar** archivos o carpetas directamente a la ventana.

### 🔊 Notificaciones por sonido
Al terminar, reproduce un sonido distinto según el resultado:
- 🔔 Éxito: sonido Asterisk.
- ⚠️ Cancelado: sonido Exclamation.
- ❌ Error: sonido Hand.

### 🧰 Bandeja del sistema
- Se queda **residente en la bandeja** (junto al reloj).
- **Cerrar con la X** lo oculta, no lo mata.
- Doble clic en el icono para abrir la ventana.
- Menú contextual con acceso rápido a todo.

### ⌨️ Atajo global: Ctrl + Alt + V
- Copia los archivos que tienes en el **portapapeles de Windows** (los que copiaste con Ctrl+C en el Explorador) directamente a la cola de SuperCopier.
- Funciona **desde cualquier ventana**, aunque SuperCopier no esté al frente.
- Flujo típico:
  1. En la ventana origen, seleccionas archivos y pulsas **Ctrl+C**.
  2. En cualquier momento pulsas **Ctrl+Alt+V**.
  3. SuperCopier aparece con todo ya cargado, listo para elegir destino e iniciar.

### 🚀 Inicio automático con Windows
- Opción en el menú de la bandeja: **"Iniciar con Windows"**.
- Se activa con un clic. No requiere permisos de administrador.
- Cuando arranca con Windows, se inicia **minimizado** en la bandeja.

### 🖱️ Integración con el menú contextual de Windows
- **Clic derecho sobre cualquier archivo** → "Copiar con SuperCopier".
- **Clic derecho sobre cualquier carpeta** → "Copiar con SuperCopier".
- **Clic derecho en el fondo de una carpeta** → "Abrir SuperCopier aquí".
- Soporta **múltiples archivos seleccionados** a la vez.

### 📝 Registro de operaciones
- Cada copia, movimiento, omisión o error queda registrado en **`SuperCopierPro.log`**.
- Ubicado junto al ejecutable, con marca de tiempo en cada línea.

### 💾 Compatibilidad
- Funciona en **Windows 7 SP1, 8.1, 10 y 11**.
- Requiere **.NET Framework 4.5 o superior** (ya incluido por defecto en Windows 10 y 11).
- Ejecutable único de **~35 KB**. No requiere instalación.

---

## 🚀 Cómo usarlo

### Uso básico
1. Abre `Super-Copier-Pro.exe`.
2. Arrastra archivos o carpetas a la lista, o usa los botones **📄 Agregar archivos** / **📁 Agregar carpeta**.
3. Elige la carpeta de destino.
4. Configura las opciones (modo copiar/mover, verificación, límite de velocidad, colisiones, reintentos).
5. Pulsa **▶ Iniciar**.

### Uso desde el menú contextual
1. Selecciona archivos o carpetas en el Explorador.
2. Clic derecho → **"Copiar con SuperCopier"**.
3. La aplicación se abre con todo ya cargado.

### Uso con el atajo global
1. Copia archivos con **Ctrl+C** en el Explorador.
2. Pulsa **Ctrl+Alt+V** desde cualquier ventana.
3. SuperCopier aparece con los archivos cargados.

### Arrancar con Windows
1. Clic derecho en el icono de la bandeja.
2. Marca **"Iniciar con Windows"**.
3. Listo. La próxima vez que enciendas la PC, estará activo en la bandeja.

---

## 🎯 Detalles técnicos

- **Lenguaje**: C# (.NET Framework 4.5).
- **Interfaz**: Windows Forms.
- **Verificación**: SHA-256 incremental mientras se copia.
- **Búfer dinámico** según el tamaño del archivo:
  - Archivos < 50 MB → búfer de 512 KB.
  - Archivos < 500 MB → búfer de 1 MB.
  - Archivos ≥ 500 MB → búfer de 4 MB.
- **Preasignación de espacio** en el destino para reducir fragmentación y detectar falta de espacio al inicio.
- **Copia a archivo temporal** (`.sc_tmp`) antes de renombrar al destino final. Así nunca queda un archivo a medias.
- **Preservación de atributos**: fecha de modificación, solo lectura, oculto, sistema y archivo.

---

## 📋 Requisitos

- Windows 7 SP1 o superior.
- .NET Framework 4.5 o superior.

---

## 👤 Creado por

**Luis Alberto**

---

## 📞 Contacto

**# 55745251**

---

## 📄 Licencia

Uso libre para fines personales y comerciales.
