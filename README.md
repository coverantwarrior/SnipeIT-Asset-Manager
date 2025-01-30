SnipeIT Asset Manager
=====================

.NET 6.0 Licencia MIT

**SnipeIT Asset Manager** es una aplicación de consola desarrollada en C# que permite gestionar activos en [Snipe-IT](https://snipeitapp.com/), un sistema de gestión de activos de código abierto. Esta herramienta facilita la creación, actualización, búsqueda y eliminación de activos directamente desde tu equipo local, integrando datos del sistema y optimizando la administración de activos de tu organización.

Tabla de Contenidos
-------------------

*   [Características](#caracteristicas)
*   [Requisitos](#requisitos)
*   [Instalación](#instalacion)
*   [Configuración](#configuracion)
*   [Uso](#uso)
    *   [Menú Principal](#menu-principal)
    *   [Opciones del Menú](#opciones-del-menu)
*   [Estructura del Código](#estructura-del-codigo)
*   [Registro de Logs](#registro-de-logs)
*   [Contribución](#contribucion)
*   [Licencia](#licencia)

Características
---------------

*   **Recopilación de Datos Locales**: Obtiene información detallada del equipo, incluyendo nombre, versión del sistema operativo, CPU, RAM, número de serie y dirección MAC.
*   **Gestión de Activos**:
    *   Crear o actualizar activos de manera inteligente.
    *   Actualizar activos existentes mediante su ID.
    *   Buscar activos en Snipe-IT por nombre, serial u otros criterios.
    *   Eliminar activos de Snipe-IT.
*   **Configuración Dinámica**: Permite configurar la URL de Snipe-IT y el token de API directamente desde la aplicación.
*   **Registro de Actividades**: Guarda un registro de las operaciones realizadas en un archivo de log (`app.log`).

Requisitos
----------

*   [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
*   Acceso a una instancia de [Snipe-IT](https://snipeitapp.com/) con permisos para gestionar activos.
*   Token de API de Snipe-IT.

Instalación
-----------

1.  **Clonar el Repositorio**:
    
        git clone https://github.com/coverantwarrior/SnipeIT-Asset-Manager.git
    
2.  **Navegar al Directorio del Proyecto**:
    
        cd SnipeIT-Asset-Manager
    
3.  **Restaurar Dependencias**:
    
        dotnet restore
    
4.  **Compilar el Proyecto**:
    
        dotnet build
    

Configuración
-------------

Antes de ejecutar la aplicación, es necesario configurar la conexión a tu instancia de Snipe-IT.

1.  **Archivo de Configuración**:
    
    Al ejecutar la aplicación por primera vez, se generará un archivo `config.json` con la configuración por defecto.
    
2.  **Editar `config.json`**:
    
    Abre el archivo `config.json` y actualiza los siguientes campos:
    
        {
          "SNIPE_IT_URL": "https://tu-snipeit.com",
          "API_TOKEN": "TU_TOKEN_DE_API"
        }
    
    *   `SNIPE_IT_URL`: URL base de tu instancia de Snipe-IT.
    *   `API_TOKEN`: Token de API generado desde tu cuenta de Snipe-IT.

Uso
---

Para iniciar la aplicación, ejecuta el siguiente comando en la terminal:

    dotnet run

### Menú Principal

Al iniciar, la aplicación presenta un menú interactivo con las siguientes opciones:

    =======================================
          GESTOR DE ACTIVOS SNIPE-IT       
    =======================================
    
    1. 🖥  Recoger y mostrar datos del equipo
    2. ➕  Crear o actualizar Asset (lógica inteligente)
    3. 🔄  Actualizar un Asset (PUT) con ID directo
    4. 🔍  Buscar activos en Snipe-IT (GET)
    5. ❌  Eliminar un activo en Snipe-IT (DELETE)
    6. ⚙️  Configuración
    7. 👋  Salir
    
    Elige una opción:

### Opciones del Menú

1.  **Recoger y Mostrar Datos del Equipo** (`1`):
    *   Muestra información local del equipo, incluyendo nombre, versión del SO, CPU, RAM, número de serie, dirección MAC y un Asset Tag sugerido.
    *   Registra la operación en `app.log`.
2.  **Crear o Actualizar Asset (Lógica Inteligente)** (`2`):
    *   Recopila datos locales del equipo.
    *   Busca en Snipe-IT si existe un activo con el mismo número de serie o nombre.
    *   Si encuentra uno, pregunta si deseas actualizarlo.
    *   Si no encuentra coincidencias o decides crear uno nuevo, procede a la creación del activo.
    *   Registra la operación en `app.log`.
3.  **Actualizar un Asset con ID Directo** (`3`):
    *   Solicita el ID del activo a actualizar.
    *   Recopila datos locales y permite modificar el nombre del equipo.
    *   Actualiza el activo en Snipe-IT con los nuevos datos.
    *   Registra la operación en `app.log`.
4.  **Buscar Activos en Snipe-IT** (`4`):
    *   Permite buscar activos por nombre, serial u otros criterios.
    *   Muestra los resultados encontrados.
    *   Registra la operación en `app.log`.
5.  **Eliminar un Activo en Snipe-IT** (`5`):
    *   Solicita el ID del activo a eliminar.
    *   Pide confirmación antes de proceder con la eliminación.
    *   Elimina el activo en Snipe-IT.
    *   Registra la operación en `app.log`.
6.  **Configuración** (`6`):
    *   Permite cambiar la URL de Snipe-IT y el token de API.
    *   Guarda los cambios en `config.json`.
    *   Registra la operación en `app.log`.
7.  **Salir** (`7`):
    *   Cierra la aplicación.

Estructura del Código
---------------------

El proyecto está estructurado de la siguiente manera:

*   **Program.cs**: Contiene la lógica principal de la aplicación, incluyendo el menú interactivo y las operaciones disponibles.
*   **Config.cs**: Define la clase de configuración para manejar los parámetros de conexión a Snipe-IT.
*   **Models**: Clases auxiliares para deserializar las respuestas JSON de la API de Snipe-IT.
*   **config.json**: Archivo de configuración que almacena la URL de Snipe-IT y el token de API.
*   **app.log**: Archivo de registro que almacena las operaciones realizadas por la aplicación.

Registro de Logs
----------------

Todas las operaciones realizadas por la aplicación se registran en el archivo `app.log` con la siguiente estructura:

    2025-01-30 12:34:56 | NombreDeOperacion | Detalles de la operación

Este registro incluye la fecha y hora de la operación, el nombre de la operación y detalles específicos de la misma.

Contribución
------------

¡Contribuciones son bienvenidas! Si deseas mejorar esta herramienta, sigue estos pasos:

1.  **Fork el Repositorio**.
2.  **Crea una Rama Nueva** para tu característica:
    
        git checkout -b feature/nueva-caracteristica
    
3.  **Commit tus Cambios**:
    
        git commit -m 'Añadir nueva característica'
    
4.  **Push a la Rama**:
    
        git push origin feature/nueva-caracteristica
    
5.  **Abre un Pull Request**.

Licencia
--------

Este proyecto está licenciado bajo la **Licencia MIT**. Consulta el archivo [LICENSE](LICENSE) para más detalles.

* * *
