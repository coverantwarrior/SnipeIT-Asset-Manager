using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SnipeIT_AssetManager
{
    class Program
    {
        private const string CONFIG_FILE = "config.json";
        private const string LOG_FILE = "app.log";  // Archivo de logs

        // Variables estáticas con la configuración
        private static Config config;

        // Endpoints calculados en base a config (para permitir cambios dinámicos)
        private static string HARDWARE_ENDPOINT => "/api/v1/hardware";
        private static string COMPANIES_ENDPOINT => "/api/v1/companies";
        private static string LOCATIONS_ENDPOINT => "/api/v1/locations";
        private static string MODELS_ENDPOINT => "/api/v1/models";
        private static string STATUS_ENDPOINT => "/api/v1/statuslabels";

        static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            // 1) Cargar (o crear) el archivo de configuración
            config = LoadOrCreateConfig();

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=======================================");
                Console.WriteLine("      GESTOR DE ACTIVOS SNIPE-IT       ");
                Console.WriteLine("=======================================\n");
                Console.WriteLine("1. 🖥  Recoger y mostrar datos del equipo");
                Console.WriteLine("2. ➕  Crear o actualizar Asset (lógica inteligente)");
                Console.WriteLine("3. 🔄  Actualizar un Asset (PUT) con ID directo");
                Console.WriteLine("4. 🔍  Buscar activos en Snipe-IT (GET)");
                Console.WriteLine("5. ❌  Eliminar un activo en Snipe-IT (DELETE)");
                Console.WriteLine("6. ⚙️  Configuración");
                Console.WriteLine("7. 👋  Salir");
                Console.Write("\nElige una opción: ");
                var opcion = Console.ReadLine();

                switch (opcion)
                {
                    case "1":
                        MostrarDatosLocales();
                        break;
                    case "2":
                        await CrearOActualizarInteligente();
                        break;
                    case "3":
                        await ActualizarPorId();
                        break;
                    case "4":
                        await BuscarActivosEnSnipeIt();
                        break;
                    case "5":
                        await EliminarAssetEnSnipeIt();
                        break;
                    case "6":
                        Configurar();
                        break;
                    case "7":
                        return;
                    default:
                        Console.WriteLine("Opción inválida. Pulsa cualquier tecla para continuar...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        #region (1) Mostrar datos locales
        private static void MostrarDatosLocales()
        {
            var datos = GetSystemData();

            Console.WriteLine("\nDatos locales del equipo:");
            Console.WriteLine($" - Nombre de equipo:   {datos["ComputerName"]}");
            Console.WriteLine($" - Versión del SO:     {datos["OSVersion"]}");
            Console.WriteLine($" - CPU:                {datos["CpuName"]}");
            Console.WriteLine($" - RAM:                {datos["Ram"]}");
            Console.WriteLine($" - Número de serie:    {datos["SerialNumber"]}");
            Console.WriteLine($" - MAC (formateada):   {datos["MacAddress"]}");
            Console.WriteLine($" - Asset Tag sugerido: {datos["AssetTag"]}");

            WriteLog("MostrarDatosLocales", $"Equipo: {datos["ComputerName"]}");
            Console.WriteLine("\nPulsa cualquier tecla para continuar...");
            Console.ReadKey();
        }
        #endregion

        #region (2) Crear o actualizar (lógica inteligente)
        /// <summary>
        /// - Pide datos locales (nombre, serial, etc.).
        /// - Busca en Snipe-IT algún activo que coincida con el Serial o el nombre de equipo.
        /// - Si encuentra alguno, pregunta si deseas actualizarlo.
        /// - Si no encuentra nada o si el usuario lo decide, crea uno nuevo.
        /// </summary>
        private static async Task CrearOActualizarInteligente()
        {
            var datos = GetSystemData();

            // Preguntar al usuario si quiere cambiar el nombre local
            Console.WriteLine("\nEl nombre del equipo detectado es: " + datos["ComputerName"]);
            Console.Write("Introduce un nuevo nombre (o deja en blanco para usar el actual): ");
            var nuevoNombre = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(nuevoNombre))
            {
                datos["ComputerName"] = nuevoNombre;
            }

            // Buscar coincidencias en Snipe-IT usando SerialNumber o ComputerName
            var serial = datos["SerialNumber"];
            var name = datos["ComputerName"];

            Console.WriteLine($"\nBuscando en Snipe-IT por Serial [{serial}] o Nombre [{name}]...");
            var encontrado = await BuscarAssetPorSerialONombre(serial, name);

            if (encontrado != null)
            {
                Console.WriteLine($"\nSe encontró un activo con ID={encontrado.id}, name={encontrado.name}, asset_tag={encontrado.asset_tag}");
                Console.Write("¿Deseas actualizarlo en lugar de crear uno nuevo? (S/N): ");
                var resp = Console.ReadLine()?.ToUpper();
                if (resp == "S")
                {
                    await ActualizarAssetFlujo(datos, encontrado.id.ToString());
                    return;
                }
            }

            // Si no se encontró nada o el usuario quiere crear un activo nuevo:
            await CrearAssetFlujo(datos);
        }
        #endregion

        #region (3) Actualizar un asset con ID directo
        private static async Task ActualizarPorId()
        {
            Console.Write("\nIntroduce el ID del Asset que quieres actualizar: ");
            var assetId = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(assetId))
            {
                Console.WriteLine("No se ha introducido un ID válido. Volviendo al menú...");
                Console.ReadKey();
                return;
            }

            var datos = GetSystemData();

            Console.WriteLine("\nEl nombre detectado es: " + datos["ComputerName"]);
            Console.Write("Introduce un nuevo nombre (o deja en blanco para usar el actual): ");
            var nuevoNombre = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(nuevoNombre))
            {
                datos["ComputerName"] = nuevoNombre;
            }

            await ActualizarAssetFlujo(datos, assetId);
        }
        #endregion

        #region (4) Buscar activos en Snipe-IT
        private static async Task BuscarActivosEnSnipeIt()
        {
            Console.Write("\nIntroduce el texto a buscar (ej. nombre, serial, etc.): ");
            var searchTerm = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                Console.WriteLine("No se ha introducido ningún criterio de búsqueda.");
                Console.ReadKey();
                return;
            }

            var endpoint = $"{HARDWARE_ENDPOINT}?search={searchTerm}";
            var jsonString = await HacerGetRequest(endpoint);
            if (jsonString == null) return; // hubo error de conexión

            var resultado = JsonConvert.DeserializeObject<SnipeItHardwareResponse>(jsonString);

            Console.WriteLine("\nResultados de la búsqueda:\n");

            if (resultado != null && resultado.rows != null && resultado.rows.Count > 0)
            {
                foreach (var row in resultado.rows)
                {
                    Console.WriteLine($"ID: {row.id}  |  Name: {row.name}  |  Asset Tag: {row.asset_tag}");
                }
            }
            else
            {
                Console.WriteLine("No se han encontrado resultados para esa búsqueda.");
            }

            WriteLog("BuscarActivos", $"Criterio: {searchTerm}");
            Console.WriteLine("\nPulsa cualquier tecla para continuar...");
            Console.ReadKey();
        }
        #endregion

        #region (5) Eliminar un activo
        private static async Task EliminarAssetEnSnipeIt()
        {
            Console.Write("\nIntroduce el ID del activo que quieres eliminar: ");
            var assetId = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(assetId))
            {
                Console.WriteLine("No se ha introducido un ID válido.");
                Console.ReadKey();
                return;
            }

            Console.Write($"Estás a punto de eliminar el activo con ID {assetId}. ¿Deseas continuar? (S/N): ");
            var confirm = Console.ReadLine()?.Trim().ToUpper();
            if (confirm != "S")
            {
                Console.WriteLine("Operación cancelada.");
                Console.ReadKey();
                return;
            }

            using var client = new HttpClient();
            client.BaseAddress = new Uri(config.SNIPE_IT_URL);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.API_TOKEN}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var deleteEndpoint = $"{HARDWARE_ENDPOINT}/{assetId}";
                var response = await client.DeleteAsync(deleteEndpoint);
                var responseString = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"\nRespuesta de Snipe-IT:\n{responseString}");
                WriteLog("EliminarAsset", $"ID: {assetId}, Respuesta: {responseString}");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("No se pudo conectar con Snipe-IT. Revisa la URL y la conexión.");
                WriteLog("EliminarAsset", $"Error de conexión al eliminar ID {assetId}");
            }

            Console.WriteLine("\nPulsa cualquier tecla para continuar...");
            Console.ReadKey();
        }
        #endregion

        #region (6) Menú de configuración
        private static void Configurar()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=======================================");
                Console.WriteLine("         MENÚ DE CONFIGURACIÓN         ");
                Console.WriteLine("=======================================\n");
                Console.WriteLine($"URL actual:        {config.SNIPE_IT_URL}");
                Console.WriteLine($"Token actual:      {config.API_TOKEN}");
                Console.WriteLine("---------------------------------------");
                Console.WriteLine("1. Cambiar URL");
                Console.WriteLine("2. Cambiar Token");
                Console.WriteLine("3. Volver al menú principal");
                Console.Write("\nElige una opción: ");
                var op = Console.ReadLine();

                if (op == "1")
                {
                    Console.Write("\nNueva URL: ");
                    var newUrl = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(newUrl))
                    {
                        config.SNIPE_IT_URL = newUrl;
                    }
                    GuardarConfig(config);
                }
                else if (op == "2")
                {
                    Console.Write("\nNuevo Token: ");
                    var newToken = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(newToken))
                    {
                        config.API_TOKEN = newToken;
                    }
                    GuardarConfig(config);
                }
                else if (op == "3")
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Opción inválida. Pulsa una tecla para continuar...");
                    Console.ReadKey();
                }
            }
        }
        #endregion

        #region Flujo de creación / actualización de assets

        /// <summary>
        /// Flujo para crear un nuevo asset usando los datos locales (más selecciones de modelo, estado, etc.).
        /// </summary>
        private static async Task CrearAssetFlujo(Dictionary<string, string> datos)
        {
            // Seleccionar modelo
            int? modelId = await SeleccionarModeloAsync();
            // Seleccionar estado
            int? statusId = await SeleccionarStatusAsync();
            // Seleccionar localización y empresa
            int? locationId = await SeleccionarLocationAsync();
            int? companyId = await SeleccionarCompanyAsync();

            // Validar campos obligatorios
            if (string.IsNullOrWhiteSpace(datos["ComputerName"]) || string.IsNullOrWhiteSpace(datos["AssetTag"]))
            {
                Console.WriteLine("\nAviso: 'name' o 'asset_tag' están vacíos. ¿Deseas corregirlos (C) o continuar (S)?");
                var resp = Console.ReadLine()?.ToUpper();
                if (resp == "C")
                {
                    Console.Write("\nNombre (name): ");
                    var n = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(n))
                        datos["ComputerName"] = n;

                    Console.Write("Asset Tag: ");
                    var t = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(t))
                        datos["AssetTag"] = t;
                }
            }

            // Preguntar fecha de compra y user_id
            Console.Write("\nIntroduce la fecha de compra (yyyy-MM-dd) o deja vacío: ");
            var purchaseDateInput = Console.ReadLine();

            Console.Write("Introduce user_id (asignado al usuario) o deja vacío: ");
            var userIdInput = Console.ReadLine();

            // Construir objeto
            var newAsset = new
            {
                name = datos["ComputerName"],
                asset_tag = datos["AssetTag"],
                serial = datos["SerialNumber"],
                model_id = modelId,
                status_id = statusId,
                location_id = locationId,
                company_id = companyId,
                user_id = string.IsNullOrWhiteSpace(userIdInput) ? null : (int?)Convert.ToInt32(userIdInput),
                purchase_date = string.IsNullOrWhiteSpace(purchaseDateInput) ? null : purchaseDateInput,
                warranty_months = 12,
                notes = $"SO: {datos["OSVersion"]}, CPU: {datos["CpuName"]}, RAM: {datos["Ram"]}, MAC: {datos["MacAddress"]}"
            };

            var respuesta = await EnviarPeticionPOST(newAsset);
            Console.WriteLine($"\nRespuesta de Snipe-IT:\n{respuesta}");
            WriteLog("CrearAsset", $"Nombre: {datos["ComputerName"]}, Respuesta: {respuesta}");
            Console.WriteLine("\nPulsa cualquier tecla para continuar...");
            Console.ReadKey();
        }

        /// <summary>
        /// Flujo para actualizar un asset existente. Se pide modelo, estado, etc. al usuario.
        /// </summary>
        private static async Task ActualizarAssetFlujo(Dictionary<string, string> datos, string assetId)
        {
            // Seleccionar modelo
            int? modelId = await SeleccionarModeloAsync();
            // Seleccionar estado
            int? statusId = await SeleccionarStatusAsync();
            // Seleccionar localización y empresa
            int? locationId = await SeleccionarLocationAsync();
            int? companyId = await SeleccionarCompanyAsync();

            // Validar campos obligatorios
            if (string.IsNullOrWhiteSpace(datos["ComputerName"]) || string.IsNullOrWhiteSpace(datos["AssetTag"]))
            {
                Console.WriteLine("\nAviso: 'name' o 'asset_tag' están vacíos. ¿Deseas corregirlos (C) o continuar (S)?");
                var resp = Console.ReadLine()?.ToUpper();
                if (resp == "C")
                {
                    Console.Write("\nNombre (name): ");
                    var n = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(n))
                        datos["ComputerName"] = n;

                    Console.Write("Asset Tag: ");
                    var t = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(t))
                        datos["AssetTag"] = t;
                }
            }

            Console.Write("\nIntroduce la fecha de compra (yyyy-MM-dd) o deja vacío: ");
            var purchaseDateInput = Console.ReadLine();

            Console.Write("Introduce user_id (asignado al usuario) o deja vacío: ");
            var userIdInput = Console.ReadLine();

            var updatedAsset = new
            {
                name = datos["ComputerName"],
                asset_tag = datos["AssetTag"],
                serial = datos["SerialNumber"],
                model_id = modelId,
                status_id = statusId,
                location_id = locationId,
                company_id = companyId,
                user_id = string.IsNullOrWhiteSpace(userIdInput) ? null : (int?)Convert.ToInt32(userIdInput),
                purchase_date = string.IsNullOrWhiteSpace(purchaseDateInput) ? null : purchaseDateInput,
                warranty_months = 12,
                notes = $"SO: {datos["OSVersion"]}, CPU: {datos["CpuName"]}, RAM: {datos["Ram"]}, MAC: {datos["MacAddress"]}"
            };

            var respuesta = await EnviarPeticionPUT(updatedAsset, assetId);
            Console.WriteLine($"\nRespuesta de Snipe-IT:\n{respuesta}");
            WriteLog("ActualizarAsset", $"ID: {assetId}, Nombre: {datos["ComputerName"]}, Respuesta: {respuesta}");
            Console.WriteLine("\nPulsa cualquier tecla para continuar...");
            Console.ReadKey();
        }
        #endregion

        #region Búsquedas auxiliares
        /// <summary>
        /// Busca si existe un asset que coincida con el serial o el nombre (usa 'search=' de Snipe-IT).
        /// Devuelve el primer resultado o null si no hay coincidencias.
        /// </summary>
        private static async Task<SnipeItHardwareRow> BuscarAssetPorSerialONombre(string serial, string name)
        {
            var endpoint = $"{HARDWARE_ENDPOINT}?search={serial} {name}";
            // Se pone ambos en un solo término. A veces conviene buscarlos por separado si se desea refinar.
            var json = await HacerGetRequest(endpoint);
            if (json == null) return null;

            var resultado = JsonConvert.DeserializeObject<SnipeItHardwareResponse>(json);
            if (resultado != null && resultado.rows != null && resultado.rows.Count > 0)
            {
                return resultado.rows[0]; // Retornamos el primero que coincida
            }
            return null;
        }
        #endregion

        #region Métodos de selección (modelo, estado, empresa, localización)
        private static async Task<int?> SeleccionarModeloAsync()
        {
            Console.WriteLine("\nObteniendo lista de modelos...");
            var url = $"{config.SNIPE_IT_URL}{MODELS_ENDPOINT}";
            var jsonString = await HacerGetRequest(url);
            if (jsonString == null) return null;

            var result = JsonConvert.DeserializeObject<SnipeItModelsResponse>(jsonString);
            if (result == null || result.rows == null || result.rows.Count == 0)
            {
                Console.WriteLine("No se encontraron modelos. Continuando sin model_id...");
                return null;
            }

            Console.WriteLine("\nModelos disponibles:");
            for (int i = 0; i < result.rows.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {result.rows[i].name} (ID={result.rows[i].id})");
            }
            Console.Write("\nElige un modelo (número) o pulsa Enter para no asignar: ");
            var eleccion = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(eleccion)) return null;
            if (int.TryParse(eleccion, out int index) && index > 0 && index <= result.rows.Count)
            {
                return result.rows[index - 1].id;
            }

            Console.WriteLine("Elección inválida. No se asignará ningún modelo.");
            return null;
        }

        private static async Task<int?> SeleccionarStatusAsync()
        {
            Console.WriteLine("\nObteniendo lista de estados (status)...");
            var url = $"{config.SNIPE_IT_URL}{STATUS_ENDPOINT}";
            var jsonString = await HacerGetRequest(url);
            if (jsonString == null) return null;

            var result = JsonConvert.DeserializeObject<SnipeItStatusResponse>(jsonString);
            if (result == null || result.rows == null || result.rows.Count == 0)
            {
                Console.WriteLine("No se encontraron status labels. Continuando sin status_id...");
                return null;
            }

            Console.WriteLine("\nEstados disponibles:");
            for (int i = 0; i < result.rows.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {result.rows[i].name} (ID={result.rows[i].id})");
            }
            Console.Write("\nElige un estado (número) o pulsa Enter para no asignar: ");
            var eleccion = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(eleccion)) return null;
            if (int.TryParse(eleccion, out int index) && index > 0 && index <= result.rows.Count)
            {
                return result.rows[index - 1].id;
            }

            Console.WriteLine("Elección inválida. No se asignará ningún estado.");
            return null;
        }

        private static async Task<int?> SeleccionarCompanyAsync()
        {
            Console.WriteLine("\nObteniendo lista de empresas...");
            var url = $"{config.SNIPE_IT_URL}{COMPANIES_ENDPOINT}";
            var jsonString = await HacerGetRequest(url);
            if (jsonString == null) return null;

            var result = JsonConvert.DeserializeObject<SnipeItCompaniesResponse>(jsonString);
            if (result == null || result.rows == null || result.rows.Count == 0)
            {
                Console.WriteLine("No se encontraron empresas. Continuando sin company_id...");
                return null;
            }

            Console.WriteLine("\nEmpresas disponibles:");
            for (int i = 0; i < result.rows.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {result.rows[i].name} (ID={result.rows[i].id})");
            }
            Console.Write("\nElige una empresa (número) o pulsa Enter para no asignar: ");
            var eleccion = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(eleccion)) return null;
            if (int.TryParse(eleccion, out int index) && index > 0 && index <= result.rows.Count)
            {
                return result.rows[index - 1].id;
            }

            Console.WriteLine("Elección inválida. No se asignará ninguna empresa.");
            return null;
        }

        private static async Task<int?> SeleccionarLocationAsync()
        {
            Console.WriteLine("\nObteniendo lista de localizaciones...");
            var url = $"{config.SNIPE_IT_URL}{LOCATIONS_ENDPOINT}";
            var jsonString = await HacerGetRequest(url);
            if (jsonString == null) return null;

            var result = JsonConvert.DeserializeObject<SnipeItLocationsResponse>(jsonString);
            if (result == null || result.rows == null || result.rows.Count == 0)
            {
                Console.WriteLine("No se encontraron localizaciones. Continuando sin location_id...");
                return null;
            }

            Console.WriteLine("\nLocalizaciones disponibles:");
            for (int i = 0; i < result.rows.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {result.rows[i].name} (ID={result.rows[i].id})");
            }
            Console.Write("\nElige una localización (número) o pulsa Enter para no asignar: ");
            var eleccion = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(eleccion)) return null;
            if (int.TryParse(eleccion, out int index) && index > 0 && index <= result.rows.Count)
            {
                return result.rows[index - 1].id;
            }

            Console.WriteLine("Elección inválida. No se asignará ninguna localización.");
            return null;
        }
        #endregion

        #region Métodos genéricos HTTP (con manejo de excepciones de red)
        private static async Task<string> HacerGetRequest(string endpoint)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(config.SNIPE_IT_URL);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.API_TOKEN}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var response = await client.GetAsync(endpoint);
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("No se pudo conectar con Snipe-IT. Revisa la URL y la conexión.");
                WriteLog("GET-Request", $"Error de conexión al GET {endpoint}");
                return null;
            }
        }

        private static async Task<string> EnviarPeticionPOST(object assetData)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(config.SNIPE_IT_URL);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.API_TOKEN}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var jsonPayload = JsonConvert.SerializeObject(assetData);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(HARDWARE_ENDPOINT, content);
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("No se pudo conectar con Snipe-IT. Revisa la URL y la conexión.");
                WriteLog("POST-Request", "Error de conexión en POST.");
                return null;
            }
        }

        private static async Task<string> EnviarPeticionPUT(object assetData, string assetId)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(config.SNIPE_IT_URL);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.API_TOKEN}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var jsonPayload = JsonConvert.SerializeObject(assetData);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var putEndpoint = $"{HARDWARE_ENDPOINT}/{assetId}";

            try
            {
                var response = await client.PutAsync(putEndpoint, content);
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("No se pudo conectar con Snipe-IT. Revisa la URL y la conexión.");
                WriteLog("PUT-Request", "Error de conexión en PUT.");
                return null;
            }
        }
        #endregion

        #region Recolección de datos locales
        private static Dictionary<string, string> GetSystemData()
        {
            var data = new Dictionary<string, string>
            {
                ["ComputerName"] = Environment.MachineName,
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["CpuName"] = string.Empty,
                ["SerialNumber"] = string.Empty,
                ["MacAddress"] = string.Empty,
                ["AssetTag"] = string.Empty,
                ["Ram"] = string.Empty
            };

            // CPU
            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    data["CpuName"] = obj["Name"]?.ToString() ?? "CPU desconocido";
                    break;
                }
            }

            // Serial
            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
            {
                foreach (var obj in searcher.Get())
                {
                    data["SerialNumber"] = obj["SerialNumber"]?.ToString() ?? "N/A";
                    break;
                }
            }

            // RAM
            ulong totalRamBytes = 0;
            using (var ramSearcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
            {
                foreach (var obj in ramSearcher.Get())
                {
                    if (ulong.TryParse(obj["Capacity"]?.ToString(), out var capacity))
                    {
                        totalRamBytes += capacity;
                    }
                }
            }
            var totalRamGB = totalRamBytes / (1024.0 * 1024.0 * 1024.0);
            data["Ram"] = totalRamGB.ToString("0.##") + " GB";

            // MAC
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            a.OperationalStatus == OperationalStatus.Up)
                .ToList();

            if (interfaces.Count == 0)
            {
                data["MacAddress"] = "";
            }
            else if (interfaces.Count == 1)
            {
                data["MacAddress"] = FormatearMac(interfaces[0].GetPhysicalAddress());
            }
            else
            {
                Console.WriteLine("\nSe han detectado múltiples interfaces de red activas:");
                for (int i = 0; i < interfaces.Count; i++)
                {
                    var macFormatted = FormatearMac(interfaces[i].GetPhysicalAddress());
                    Console.WriteLine($"{i + 1}. {interfaces[i].Name} - {macFormatted}");
                }

                Console.Write("\nSelecciona un número para elegir esa MAC o escribe 'todas' para concatenarlas: ");
                var eleccion = Console.ReadLine()?.Trim().ToLower();

                if (eleccion == "todas")
                {
                    var listaTodas = interfaces.Select(i => FormatearMac(i.GetPhysicalAddress()));
                    data["MacAddress"] = string.Join("; ", listaTodas);
                }
                else
                {
                    if (int.TryParse(eleccion, out int index) && index > 0 && index <= interfaces.Count)
                    {
                        data["MacAddress"] = FormatearMac(interfaces[index - 1].GetPhysicalAddress());
                    }
                    else
                    {
                        data["MacAddress"] = FormatearMac(interfaces[0].GetPhysicalAddress());
                    }
                }
            }

            // Generar un Asset Tag por defecto
            data["AssetTag"] = $"PC-{data["ComputerName"]}-{DateTime.Now:yyyyMMdd}";

            return data;
        }

        private static string FormatearMac(PhysicalAddress physicalAddress)
        {
            var macBytes = physicalAddress.GetAddressBytes();
            return string.Join(":", macBytes.Select(b => b.ToString("X2")));
        }
        #endregion

        #region Config y Log
        /// <summary>
        /// Carga o crea un archivo config.json
        /// </summary>
        private static Config LoadOrCreateConfig()
        {
            if (!File.Exists(CONFIG_FILE))
            {
                Console.WriteLine($"No se encontró '{CONFIG_FILE}'. Creando archivo de configuración por defecto...");

                var defaultConfig = new Config
                {
                    SNIPE_IT_URL = "https://example-snipeit.com",
                    API_TOKEN = "TU_TOKEN_DE_API"
                };

                var defaultJson = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                File.WriteAllText(CONFIG_FILE, defaultJson, Encoding.UTF8);
            }

            var json = File.ReadAllText(CONFIG_FILE, Encoding.UTF8);
            var cfg = JsonConvert.DeserializeObject<Config>(json);

            if (string.IsNullOrWhiteSpace(cfg.SNIPE_IT_URL) || string.IsNullOrWhiteSpace(cfg.API_TOKEN))
            {
                Console.WriteLine("El archivo config.json está incompleto. Ajusta los valores y reinicia.");
                Console.WriteLine("Pulsa cualquier tecla para salir...");
                Console.ReadKey();
                Environment.Exit(0);
            }

            return cfg;
        }

        /// <summary>
        /// Guarda la configuración actual en config.json
        /// </summary>
        private static void GuardarConfig(Config cfg)
        {
            var json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
            File.WriteAllText(CONFIG_FILE, json, Encoding.UTF8);
            Console.WriteLine("\nConfiguración guardada correctamente.");
            WriteLog("GuardarConfig", $"Nuevos valores: URL={cfg.SNIPE_IT_URL}, TOKEN={cfg.API_TOKEN}");
            Console.WriteLine("\nPulsa cualquier tecla para continuar...");
            Console.ReadKey();
        }

        /// <summary>
        /// Escribe un registro en app.log con fecha/hora y detalles de la operación.
        /// </summary>
        private static void WriteLog(string operation, string details)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {operation} | {details}";
            File.AppendAllLines(LOG_FILE, new[] { line }, Encoding.UTF8);
        }
        #endregion
    }

    #region Clases auxiliares para parsear JSON
    public class Config
    {
        public string SNIPE_IT_URL { get; set; }
        public string API_TOKEN { get; set; }
    }

    public class SnipeItHardwareResponse
    {
        public int total { get; set; }
        public List<SnipeItHardwareRow> rows { get; set; }
    }

    public class SnipeItHardwareRow
    {
        public int id { get; set; }
        public string name { get; set; }
        public string asset_tag { get; set; }
    }

    public class SnipeItCompaniesResponse
    {
        public int total { get; set; }
        public List<SnipeItCompanyRow> rows { get; set; }
    }

    public class SnipeItCompanyRow
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class SnipeItLocationsResponse
    {
        public int total { get; set; }
        public List<SnipeItLocationRow> rows { get; set; }
    }

    public class SnipeItLocationRow
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class SnipeItModelsResponse
    {
        public int total { get; set; }
        public List<SnipeItModelRow> rows { get; set; }
    }

    public class SnipeItModelRow
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class SnipeItStatusResponse
    {
        public int total { get; set; }
        public List<SnipeItStatusRow> rows { get; set; }
    }

    public class SnipeItStatusRow
    {
        public int id { get; set; }
        public string name { get; set; }
    }
    #endregion
}
