using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Arena", "SCR98", "2.0.0")]
    [Description("Arena de NPC con aspecto de jugador que corren y esquivan obstáculos. Todo se controla desde un panel.")]
    class Arena : RustPlugin
    {
        #region Constantes

        private const string PermAdmin = "arena.admin";
        private const string UiFondo = "Arena.Fondo";
        private const string UiContenido = "Arena.Contenido";
        private const string UiCardEstado = "Arena.CardEstado";
        private const string UiVivos = "Arena.Vivos";
        private const string UiMarcador = "Arena.Marcador";
        private const string Prefijo = "<color=#f78a1f>[Arena]</color> ";

        // Prefab de jugador normal: el NPC se ve como un personaje, no como un científico
        // (mismo prefab que usa MyNPC: NPCPlayer con modelo de jugador)
        private const string PrefabMini = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string PrefabJugador = "assets/prefabs/player/player.prefab";

        private const string FuenteNormal = "robotocondensed-regular.ttf";
        private const string FuenteNegrita = "robotocondensed-bold.ttf";

        // Paleta: tema claro y naranja (igual que ElTiempo)
        private const string ColFondo = "0.10 0.07 0.04 0.55";
        private const string ColPanel = "0.992 0.972 0.945 1";
        private const string ColTarjeta = "1 1 1 1";
        private const string ColNaranja = "0.97 0.52 0.12 1";
        private const string ColNaranjaOsc = "0.78 0.36 0.04 1";
        private const string ColNaranjaClaro = "1 0.925 0.83 1";
        private const string ColTexto = "0.22 0.18 0.15 1";
        private const string ColSuave = "0.55 0.48 0.42 1";
        private const string ColApagado = "0.94 0.92 0.90 1";
        private const string ColBlanco = "1 1 1 1";
        private const string ColNegro = "0 0 0 1";
        private const string ColBlancoSuave = "1 1 1 0.85";

        // Movimiento de los NPC
        private const float Gravedad = 20f;   // saltos secos, no flotando
        private const float ImpulsoSalto = 7.5f;   // solo de reserva: el salto real se calcula segun la altura
        private const float AlturaMaxSalto = 1.5f; // obstaculos mas altos se rodean (los mas bajos se saltan o escalan)
        private const float AlturaMini = 35f;      // altura de vuelo sobre el terreno

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly float[] AngulosEsquivar = { 35f, 70f, 110f, 150f };
        private static readonly float[] AngulosVuelo = { 40f, 80f, 120f };

        #endregion

        #region Configuracion y datos

        private class PluginConfig
        {
            public int CantQuietos = 25;          // se quedan parados
            public int CantCorriendo = 25;        // corren por su zona
            public int CantCaminando = 0;         // caminan por su zona
            public int CantMinis = 2;             // minihelicopteros con 1 persona (solo piloto)
            public int CantMinisDobles = 1;       // minihelicopteros con 2 personas (piloto y pasajero)
            public float Radius = 200f;
            public int Rings = 10;                // anillos de reparto (10 = cada 10% del radio)
            public float RespawnSeconds = 5f;
            public bool InfinitePlayerAmmo = true;
            public bool NoWeaponWear = true;
            public bool PlayerImmortal = true;    // tu: no recibes dano ni te baja hambre/sed
            public bool BotsImmortal = false;     // los bots no reciben dano
            public float RunSpeed = 5.5f;
            public float WalkSpeed = 2.8f;
            public float MiniSpeed = 18f;
            public float MinSpawnDistance = 25f;   // distancia minima a los jugadores al aparecer (m)
            public bool RoamFullArena = true;      // true: recorren toda la arena; false: solo su zona
            public bool MiniMotorEncendido = true; // helices girando en los minihelicopteros
            public bool SpawnVisible = false;      // los NPC aparecen donde tu puedas verlos (y ellos a ti)
            public bool ShowHitDistance = true;    // marcador con la distancia al NPC al que aciertas
            public int HitCorner = 1;              // 0 arriba izq. | 1 arriba der. | 2 abajo der. | 3 abajo izq.
            public bool RemoveAnimals = true;      // elimina los animales que haya dentro de la arena (mientras esta activa)
            public int ConfigVersion = 0;          // para migrar ajustes de versiones anteriores
        }

        private class OutfitSet
        {
            public string Name;
            public List<ItemData> Items = new List<ItemData>();
        }

        private class ItemData
        {
            public string ShortName;
            public ulong Skin;
            public int Amount;
            public string Container;
            public int Slot;
        }

        private PluginConfig config;

        private readonly Dictionary<string, OutfitSet> outfits =
            new Dictionary<string, OutfitSet>(StringComparer.OrdinalIgnoreCase);

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig { ConfigVersion = 4 };
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null) throw new Exception();

                // Migracion: municion infinita, sin desgaste e inmortal pasan a estar activados por defecto
                if (config.ConfigVersion < 2)
                {
                    config.InfinitePlayerAmmo = true;
                    config.NoWeaponWear = true;
                    config.PlayerImmortal = true;
                    config.ConfigVersion = 2;
                    SaveConfig();
                }

                // Migracion: se aplican una sola vez los nuevos valores por defecto
                if (config.ConfigVersion < 3)
                {
                    config.CantQuietos = 25;
                    config.CantCorriendo = 25;
                    config.CantCaminando = 0;
                    config.CantMinis = 3;
                    config.Radius = 200f;
                    config.MinSpawnDistance = 25f;
                    config.Rings = 10;
                    config.RespawnSeconds = 5f;
                    config.InfinitePlayerAmmo = true;
                    config.NoWeaponWear = true;
                    config.PlayerImmortal = true;
                    config.BotsImmortal = false;
                    config.SpawnVisible = false;
                    config.ShowHitDistance = true;
                    config.HitCorner = 1; // arriba a la derecha
                    config.ConfigVersion = 3;
                    SaveConfig();
                }

                // Migracion: minihelicopteros de 1 persona (2) y de 2 personas (1)
                if (config.ConfigVersion < 4)
                {
                    config.CantMinis = 2;
                    config.CantMinisDobles = 1;
                    config.ConfigVersion = 4;
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning("Configuración no válida. Se creará una nueva.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void LoadOutfits()
        {
            outfits.Clear();

            string archivo = Name + "_Outfits";

            // Migracion: si no hay datos nuevos, se usan los del plugin antiguo
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(archivo) &&
                Interface.Oxide.DataFileSystem.ExistsDatafile("RandomNPCArena_Outfits"))
            {
                archivo = "RandomNPCArena_Outfits";
            }

            Dictionary<string, OutfitSet> stored;
            try { stored = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, OutfitSet>>(archivo); }
            catch { stored = null; }

            if (stored == null) return;

            foreach (var entry in stored)
            {
                if (entry.Value != null) outfits[entry.Key] = entry.Value;
            }

            if (archivo != Name + "_Outfits") SaveOutfits();
        }

        private void SaveOutfits()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "_Outfits", outfits);
        }

        #endregion

        #region Estado de la arena

        private enum TipoBot { Quieto, Correr, Caminar, Mini, MiniDoble }

        private static readonly TipoBot[] TodosLosTipos =
            { TipoBot.Quieto, TipoBot.Correr, TipoBot.Caminar, TipoBot.Mini, TipoBot.MiniDoble };

        private class Bot
        {
            public TipoBot Tipo;
            public bool Roto;             // dio un error al moverse: se retira
            public Rigidbody Cuerpo;      // fisicas del mini (se mantienen desactivadas)
            public BaseVehicle Mini;      // solo para los pilotos
            public BasePlayer Npc;
            public Vector3 Meta;
            public bool TieneMeta;
            public float MetaHasta;
            public float Lado = 1f;       // hacia que lado prefiere rodear
            public float VelY;
            public bool EnAire;
            public float SaltoEspera;
            public float Atasco;
            public Vector3 Mira = Vector3.forward;
            public float SondeoEspera;    // los sondeos de obstaculos no se hacen en cada tick
            public Vector3 DirEsq = Vector3.forward;
            public bool SaltoEsq;
            public float EsquivarHasta;   // mantiene un rodeo un momento para no zigzaguear
            public int Slot;              // hueco de la arena (decide su anillo y su sector)
            public Vector3 Home;          // donde aparecio: patrulla por esa zona
            public int Lod = 1;           // 1 = cada frame; mas alto = piensa menos veces (lejos de los jugadores)
            public float AcumDt;
            public float UltimoMov;       // ultimo paso de movimiento (cada NPC piensa a su ritmo)
            public float UltimaRed;
            public float UltimaCompleta;
            public float UltimaMotor;
            public Vector3 PosGrupo;
            // Vuelo del mini
            public float Giro;            // rumbo en grados
            public float VelGiro;         // grados por segundo
            public float Vel;             // velocidad hacia delante
            public float VelV;            // velocidad vertical
            public float Cabeceo;
            public float Alabeo;
            public float AltVuelo = 35f;  // altura sobre el terreno que prefiere este piloto
            public float AltObjetivo;
            public float Desvio;          // giro extra para esquivar
            public float DesvioHasta;
            public bool Frenar;
            public float AltoSalto = 1f;  // altura del obstaculo que va a saltar
            public Vector3 VelH;          // velocidad horizontal real (con inercia: derrapa un poco al girar)
            public float AltSuave;        // altura objetivo suavizada (no salta de golpe)
            public float Fase;            // desfase propio para el balanceo de cada piloto
            public BasePlayer Pasajero;   // solo en MiniDoble: segunda persona del mini
        }

        private class Pendiente
        {
            public TipoBot Tipo;
            public int Slot;
            public float Hora;
            public int Fallos;            // intentos sin sitio visible (tras varios se relaja la condicion)
        }

        private readonly List<Bot> bots = new List<Bot>();
        private readonly HashSet<BasePlayer> botNpcs = new HashSet<BasePlayer>();
        private readonly Dictionary<BaseEntity, Bot> minis = new Dictionary<BaseEntity, Bot>();
        private readonly List<Pendiente> pendientes = new List<Pendiente>(); // huecos que toca volver a rellenar

        private bool arenaActiva;
        private ulong idJugadorArena;     // quien inicio la arena: los NPC quietos lo miran a el
        private int erroresMini;
        private bool errorMostrado;
        private Vector3 centro;
        private Timer arenaTimer;
        private float ultimoTick;
        private int contadorTick;
        private int contadorFrame;
        private readonly List<KeyValuePair<Vector3, float>> muertes = new List<KeyValuePair<Vector3, float>>();
        private int contadorNpc;
        private ulong idUnico;

        private int maskObstaculos;
        private int maskSuelo;
        private int maskVuelo;
        private int maskArboles;

        // Interfaz
        private readonly HashSet<ulong> abiertos = new HashSet<ulong>();
        private readonly Dictionary<ulong, string> ultimoVivos = new Dictionary<ulong, string>();
        private Timer tickUi;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            LoadOutfits();

            maskObstaculos = LayerMask.GetMask("World", "Construction", "Deployed", "Tree",
                "Vehicle Detailed", "Vehicle Large");
            maskSuelo = LayerMask.GetMask("Terrain", "World", "Construction");
            // Obstaculos para el helicoptero (sin vehiculos, para no chocar con el propio mini)
            maskVuelo = LayerMask.GetMask("Terrain", "World", "Construction", "Deployed", "Tree");            // Solo troncos de arbol (las hojas no tienen colision en el servidor)
            maskArboles = LayerMask.GetMask("Tree");
        }

        private void OnServerInitialized()
        {
            LimpiarRestos();
            timer.Every(1f, MantenerJugadores);

            // Lista de animales del mapa (una sola busqueda; despues se mantiene con OnEntitySpawned)
            EscanearAnimales();
        }

        // Modo inmortal del jugador: vida, comida y agua siempre al maximo (solo con la arena activa)
        private void MantenerJugadores()
        {
            if (!arenaActiva || !config.PlayerImmortal) return;

            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (p == null || !p.IsConnected || p.IsDead() || botNpcs.Contains(p) || !TienePermiso(p)) continue;

                PlayerMetabolism m = p.metabolism;
                if (m != null)
                {
                    m.calories.value = m.calories.max;
                    m.hydration.value = m.hydration.max;
                }

                if (p.health < p.MaxHealth()) p.SetHealth(p.MaxHealth());
            }
        }

        private void Unload()
        {
            DetenerArena();
            LimpiarRestos();

            foreach (var jugador in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(jugador, UiFondo);
                CuiHelper.DestroyUi(jugador, UiMarcador);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            abiertos.Remove(player.userID);
            ultimoVivos.Remove(player.userID);
            ultimoMarcador.Remove(player.userID);
            timersMarcador.Remove(player.userID);
            AjustarTick();
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer npc = entity as BasePlayer;
            if (npc == null || !botNpcs.Contains(npc)) return;

            // El arma no se suelta: se quita de la barra antes de morir (solo bots de la arena)
            if (npc.inventory != null)
            {
                foreach (Item objeto in new List<Item>(npc.inventory.containerBelt.itemList))
                    objeto.Remove();
                ItemManager.DoRemoves();
            }

            // Se apunta donde murio, por si algun objeto llega a caer igualmente
            float ahoraMuerte = Time.realtimeSinceStartup;
            muertes.RemoveAll(m => ahoraMuerte - m.Value > 4f);
            muertes.Add(new KeyValuePair<Vector3, float>(npc.transform.position, ahoraMuerte));

            Bot muerto = null;
            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i].Npc == npc)
                {
                    muerto = bots[i];
                    bots.RemoveAt(i);
                    break;
                }
            }
            botNpcs.Remove(npc);

            // Si era el pasajero de un mini, solo se anota (el mini sigue y el pasajero no reaparece solo)
            foreach (Bot bp in bots)
                if (bp.Pasajero == npc) bp.Pasajero = null;
            foreach (Caida cp in caidas)
                if (cp.Pasajero == npc) cp.Pasajero = null;

            if (muerto != null)
            {
                if (muerto.Tipo == TipoBot.MiniDoble && muerto.Mini != null && !muerto.Mini.IsDestroyed)
                    IniciarCaida(muerto); // sin piloto el mini cae como un helicoptero de verdad
                else
                    EliminarMini(muerto); // el mini de 1 persona se elimina sin dejar nada
            }

            // El jugador muerto no se borra solo: se elimina poco despues
            timer.Once(1.5f, () =>
            {
                if (npc != null && !npc.IsDestroyed) npc.Kill();
            });

            // Reaparece en el mismo hueco (mismo anillo y sector) para mantener el reparto
            if (arenaActiva && muerto != null)
                pendientes.Add(new Pendiente
                {
                    Tipo = muerto.Tipo,
                    Slot = muerto.Slot,
                    Hora = Time.realtimeSinceStartup + config.RespawnSeconds
                });
        }

        // El minihelicoptero de un piloto no se rompe ni transmite el dano al piloto.
        // Cuando el piloto muere, el mini desaparece sin explotar ni dejar nada.
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            BasePlayer jugador = entity as BasePlayer;
            if (jugador != null)
            {
                if (botNpcs.Contains(jugador))
                {
                    // El pasajero de un mini destruido muere de verdad (aunque los bots sean inmortales)
                    if (matandoPasajero) return null;

                    // Animales, cientificos, Bradley y helicoptero del mapa no pueden danar a los bots
                    if (EsIaDelMapa(info.Initiator)) return true;

                    // Marcador: distancia a la que esta el NPC que acaba de recibir tu golpe
                    BasePlayer atacante = info.InitiatorPlayer;
                    if (config.ShowHitDistance && atacante != null && atacante != jugador &&
                        !botNpcs.Contains(atacante) && atacante.IsConnected && TienePermiso(atacante))
                        MostrarMarcador(atacante, Vector3.Distance(atacante.transform.position, jugador.transform.position));

                    if (config.BotsImmortal) return true;

                    return null;
                }
                if (arenaActiva && config.PlayerImmortal && TienePermiso(jugador)) return true;
                return null;
            }

            if (caidas.Count > 0)
            {
                foreach (Caida cd in caidas)
                    if (cd.Mini == entity) return DanoMiniCaida(cd, entity, info);
            }

            if (minis.Count == 0) return null;

            // El mini con piloto no recibe dano ni lo pasa al piloto: al piloto solo se le hace dano disparandole a el
            if (!minis.ContainsKey(entity)) return null;

            return true;
        }

        #region Marcador de distancia

        private readonly Dictionary<ulong, Timer> timersMarcador = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, float> ultimoMarcador = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, string> textoMarcador = new Dictionary<ulong, string>();

        // Dibuja el cuadro (borde naranja, fondo blanco, letras negras) en la esquina elegida.
        // Con la arena activa se queda fijo mostrando la ultima distancia (en blanco hasta el primer golpe).
        private void DibujarMarcador(BasePlayer jugador, string texto)
        {
            if (jugador == null || !jugador.IsConnected) return;

            // Tamano fijo en pixeles (150 x 44) pegado a la esquina con 12 px de margen:
            // asi el borde tiene el mismo grosor en los cuatro lados en cualquier pantalla.
            string anclaMin, anclaMax, offMin, offMax;
            switch (config.HitCorner)
            {
                case 0: anclaMin = anclaMax = "0 1"; offMin = "12 -56"; offMax = "162 -12"; break;   // arriba izquierda
                case 2: anclaMin = anclaMax = "1 0"; offMin = "-162 12"; offMax = "-12 56"; break;   // abajo derecha
                case 3: anclaMin = anclaMax = "0 0"; offMin = "12 12"; offMax = "162 56"; break;     // abajo izquierda
                default: anclaMin = anclaMax = "1 1"; offMin = "-162 -56"; offMax = "-12 -12"; break; // arriba derecha
            }

            const string Fondo = UiMarcador + ".Fondo";

            var c = new CuiElementContainer();

            // Borde naranja (el elemento exterior)
            c.Add(new CuiElement
            {
                Name = UiMarcador,
                Parent = "Overlay", // por encima de la vida, el inventario y el resto de la interfaz
                DestroyUi = UiMarcador,
                Components =
                {
                    new CuiImageComponent { Color = ColNaranja },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anclaMin, AnchorMax = anclaMax, OffsetMin = offMin, OffsetMax = offMax
                    }
                }
            });

            // Fondo blanco: ocupa todo el cuadro menos 4 px de borde por cada lado
            c.Add(new CuiElement
            {
                Name = Fondo,
                Parent = UiMarcador,
                Components =
                {
                    new CuiImageComponent { Color = ColBlanco },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4"
                    }
                }
            });

            // Texto negro centrado en el fondo blanco
            c.Add(new CuiElement
            {
                Parent = Fondo,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = texto,
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter,
                        Color = ColNegro,
                        Font = FuenteNegrita
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(jugador, c);
        }

        // Golpe a un NPC: se guarda y se muestra la distancia
        private void MostrarMarcador(BasePlayer jugador, float distancia)
        {
            if (jugador == null || !jugador.IsConnected) return;

            ulong id = jugador.userID;
            float ahora = Time.realtimeSinceStartup;

            // Con armas automaticas o escopetas llegan muchos golpes seguidos: se limita la frecuencia
            float ultimo;
            if (ultimoMarcador.TryGetValue(id, out ultimo) && ahora - ultimo < 0.05f) return;
            ultimoMarcador[id] = ahora;

            string texto = distancia.ToString("0.0", Inv) + " m";
            textoMarcador[id] = texto;
            DibujarMarcador(jugador, texto);
        }

        private bool MarcadorVisible()
        {
            return arenaActiva && config.ShowHitDistance;
        }

        // Pone o quita el cuadro a todos los jugadores con permiso segun el estado actual
        private void RefrescarMarcadores()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (p == null || !p.IsConnected || botNpcs.Contains(p) || !TienePermiso(p)) continue;

                if (MarcadorVisible())
                {
                    string texto;
                    if (!textoMarcador.TryGetValue(p.userID, out texto)) texto = "";
                    DibujarMarcador(p, texto);
                }
                else
                {
                    CuiHelper.DestroyUi(p, UiMarcador);
                }
            }
        }

        // Vista previa al cambiar la esquina con la arena parada (2.5 s)
        private void VistaPreviaMarcador(BasePlayer jugador)
        {
            if (MarcadorVisible())
            {
                RefrescarMarcadores();
                return;
            }

            DibujarMarcador(jugador, "123.4 m");

            ulong id = jugador.userID;
            Timer previo;
            if (timersMarcador.TryGetValue(id, out previo) && previo != null && !previo.Destroyed) previo.Destroy();

            timersMarcador[id] = timer.Once(2.5f, () =>
            {
                if (jugador != null && jugador.IsConnected && !MarcadorVisible())
                    CuiHelper.DestroyUi(jugador, UiMarcador);
            });
        }

        // Al reaparecer el cuadro se vuelve a poner
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || !MarcadorVisible() || !TienePermiso(player)) return;

            string texto;
            if (!textoMarcador.TryGetValue(player.userID, out texto)) texto = "";
            DibujarMarcador(player, texto);
        }

        #endregion

        private bool EsIaDelMapa(BaseEntity e)
        {
            return e != null && (e is BaseNpc || e is NPCPlayer || e is BradleyAPC || e is BaseHelicopter);
        }

        // ---- Animales: no hay forma fiable de impedir que persigan a un bot, asi que se quitan de la arena ----
        // Se reconocen por la ruta del prefab (no por el tipo de clase, que cambia entre versiones de Rust).
        private readonly HashSet<BaseEntity> animales = new HashSet<BaseEntity>();
        private readonly List<BaseEntity> animalesFuera = new List<BaseEntity>();

        private bool EsAnimal(BaseNetworkable e)
        {
            BaseCombatEntity c = e as BaseCombatEntity;
            if (c == null || c is BasePlayer || c is BaseCorpse) return false;

            string ruta = c.PrefabName;
            if (string.IsNullOrEmpty(ruta) || ruta.IndexOf("rust.ai/agents/", StringComparison.Ordinal) < 0) return false;
            if (ruta.IndexOf("corpse", StringComparison.Ordinal) >= 0 ||
                ruta.IndexOf("ragdoll", StringComparison.Ordinal) >= 0 ||
                ruta.IndexOf("horse", StringComparison.Ordinal) >= 0) return false;

            return true;
        }

        private void EscanearAnimales()
        {
            foreach (BaseNetworkable n in BaseNetworkable.serverEntities)
            {
                if (EsAnimal(n)) animales.Add((BaseEntity)n);
            }
        }

        private void LimpiarAnimales()
        {
            animales.RemoveWhere(a => a == null || a.IsDestroyed);

            float limite = config.Radius + 60f;
            limite *= limite;

            animalesFuera.Clear();
            foreach (BaseEntity a in animales)
            {
                float dx = a.transform.position.x - centro.x;
                float dz = a.transform.position.z - centro.z;
                if (dx * dx + dz * dz <= limite) animalesFuera.Add(a);
            }

            foreach (BaseEntity a in animalesFuera)
            {
                if (a != null && !a.IsDestroyed) a.Kill();
            }
            animalesFuera.Clear();
        }

        // Los animales y los NPC del mapa no ven a los bots de la arena como objetivo
        private object OnNpcTarget(BaseNpc npc, BaseEntity target)
        {
            BasePlayer p = target as BasePlayer;
            if (p != null && botNpcs.Contains(p)) return true;
            return null;
        }

        private object OnNpcTarget(NPCPlayer npc, BaseEntity target)
        {
            BasePlayer p = target as BasePlayer;
            if (p != null && botNpcs.Contains(p)) return true;
            return null;
        }

        // Un animal tampoco puede atacar a un bot aunque lo haya seguido
        private object CanNpcAttack(BaseNpc npc, BaseEntity target)
        {
            BasePlayer p = target as BasePlayer;
            if (p != null && botNpcs.Contains(p)) return false;
            return null;
        }

        // Los bots de la arena nunca sueltan lo que llevan en la mano
        private object CanDropActiveItem(BasePlayer player)
        {
            if (player != null && botNpcs.Contains(player)) return false;
            return null;
        }

        // Red de seguridad: cualquier objeto que caiga junto a un bot recien muerto se elimina
        private void OnEntitySpawned(BaseEntity objeto)
        {
            if (objeto == null) return;
            if (EsAnimal(objeto)) { animales.Add(objeto); return; }

            if (muertes.Count == 0) return;
            if (!(objeto is WorldItem) && !(objeto is DroppedItemContainer)) return;

            Vector3 donde = objeto.transform.position;
            float ahora = Time.realtimeSinceStartup;

            foreach (var m in muertes)
            {
                if (ahora - m.Value <= 4f && (m.Key - donde).sqrMagnitude < 25f)
                {
                    NextTick(() =>
                    {
                        if (objeto != null && !objeto.IsDestroyed) objeto.Kill();
                    });
                    return;
                }
            }
        }

        // El cuerpo de un NPC de la arena desaparece nada mas crearse
        private void OnEntitySpawned(LootableCorpse corpse)
        {
            if (corpse == null) return;

            NextTick(() =>
            {
                if (corpse == null || corpse.IsDestroyed) return;

                string nombre = corpse.playerName;
                if (nombre != null && nombre.StartsWith("Arena NPC")) corpse.Kill();
            });
        }

        // Los NPC mueren del todo en vez de quedarse heridos en el suelo
        private object OnPlayerWound(BasePlayer player, HitInfo info)
        {
            if (player != null && botNpcs.Contains(player)) return false;
            return null;
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            RecargarMunicion(attacker);
            return null;
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod,
            ProtoBuf.ProjectileShoot projectiles)
        {
            RecargarMunicion(player);
        }

        // Las armas no se desgastan mientras la arena esta activa
        private void OnLoseCondition(Item item, ref float amount)
        {
            if (!arenaActiva || !config.NoWeaponWear || item == null) return;
            if (item.GetHeldEntity() == null) return;

            BasePlayer dueno = item.GetOwnerPlayer();
            if (dueno == null || botNpcs.Contains(dueno) || !TienePermiso(dueno)) return;

            amount = 0f;
        }

        private void RecargarMunicion(BasePlayer jugador)
        {
            if (!arenaActiva || !config.InfinitePlayerAmmo) return;
            if (jugador == null || botNpcs.Contains(jugador) || !TienePermiso(jugador)) return;

            BaseProjectile arma = jugador.GetHeldEntity() as BaseProjectile;
            if (arma == null || arma.primaryMagazine == null) return;

            if (arma.primaryMagazine.contents < arma.primaryMagazine.capacity)
            {
                arma.primaryMagazine.contents = arma.primaryMagazine.capacity;
                arma.SendNetworkUpdateImmediate();
            }
        }

        #endregion

        #region Comandos

        [ChatCommand("arena")]
        private void CmdArena(BasePlayer player, string command, string[] args)
        {
            if (!TienePermiso(player))
            {
                SendReply(player, Prefijo + "No tienes permiso para usar este comando.");
                return;
            }
            MostrarUi(player, true);
        }

        [ConsoleCommand("arena.ui")]
        private void CmdUi(ConsoleSystem.Arg arg)
        {
            var jugador = arg.Player();
            if (jugador == null || !TienePermiso(jugador)) return;

            // En versiones nuevas de Rust FullString es un StringView, asi que se une desde Args
            string completo = arg.HasArgs() ? string.Join(" ", arg.Args).Trim() : "";
            int espacio = completo.IndexOf(' ');
            string accion = (espacio < 0 ? completo : completo.Substring(0, espacio)).ToLowerInvariant();
            string resto = espacio < 0 ? "" : completo.Substring(espacio + 1).Trim().Trim('"');

            string[] tok = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string a1 = tok.Length > 0 ? tok[0] : "";
            string a2 = tok.Length > 1 ? tok[1] : "";

            switch (accion)
            {
                case "cerrar":
                    CuiHelper.DestroyUi(jugador, UiFondo);
                    abiertos.Remove(jugador.userID);
                    ultimoVivos.Remove(jugador.userID);
                    AjustarTick();
                    return;

                case "refrescar":
                    break;

                case "iniciar":
                    IniciarArena(jugador);
                    break;

                case "detener":
                    DetenerArena();
                    break;

                case "set":
                    {
                        float v;
                        if (float.TryParse(a2.Replace(',', '.'), NumberStyles.Float, Inv, out v))
                            AplicarCampo(a1, v);
                    }
                    break;

                case "paso":
                    {
                        float delta;
                        if (float.TryParse(a2.Replace(',', '.'), NumberStyles.Float, Inv, out delta))
                            AplicarCampo(a1, ValorCampo(a1) + delta);
                    }
                    break;

                case "municion":
                    config.InfinitePlayerAmmo = !config.InfinitePlayerAmmo;
                    SaveConfig();
                    break;

                case "desgaste":
                    config.NoWeaponWear = !config.NoWeaponWear;
                    SaveConfig();
                    break;

                case "inmortal":
                    config.PlayerImmortal = !config.PlayerImmortal;
                    SaveConfig();
                    break;

                case "marcador":
                    config.ShowHitDistance = !config.ShowHitDistance;
                    SaveConfig();
                    if (config.ShowHitDistance) VistaPreviaMarcador(jugador);
                    else RefrescarMarcadores();
                    break;

                case "esquina":
                    {
                        int esq;
                        if (int.TryParse(a1, out esq))
                        {
                            config.HitCorner = Mathf.Clamp(esq, 0, 3);
                            SaveConfig();
                            VistaPreviaMarcador(jugador); // cuadro en la nueva esquina
                        }
                    }
                    break;

                case "visible":
                    config.SpawnVisible = !config.SpawnVisible;
                    SaveConfig();
                    break;

                case "botsinmortales":
                    config.BotsImmortal = !config.BotsImmortal;
                    SaveConfig();
                    break;

                case "guardar":
                    GuardarRopa(jugador, resto);
                    break;

                case "borrar":
                    {
                        int idx;
                        var nombres = NombresConjuntos();
                        if (int.TryParse(a1, out idx) && idx >= 0 && idx < nombres.Count)
                        {
                            outfits.Remove(nombres[idx]);
                            SaveOutfits();
                        }
                    }
                    break;
            }

            MostrarUi(jugador);
        }

        #endregion

        #region Ajustes

        private bool TienePermiso(BasePlayer jugador)
        {
            return jugador != null &&
                   (jugador.IsAdmin || permission.UserHasPermission(jugador.UserIDString, PermAdmin));
        }

        private int Cantidad(TipoBot tipo)
        {
            switch (tipo)
            {
                case TipoBot.Quieto: return config.CantQuietos;
                case TipoBot.Correr: return config.CantCorriendo;
                case TipoBot.Caminar: return config.CantCaminando;
                case TipoBot.Mini: return config.CantMinis;
                default: return config.CantMinisDobles;
            }
        }

        private static bool EsMini(TipoBot t)
        {
            return t == TipoBot.Mini || t == TipoBot.MiniDoble;
        }

        // NPC vivos: cada mini de 2 personas cuenta 2 mientras su pasajero siga vivo
        private int NpcVivos()
        {
            int n = 0;
            foreach (Bot b in bots)
            {
                n++;
                if (b.Pasajero != null && !b.Pasajero.IsDestroyed && !b.Pasajero.IsDead()) n++;
            }
            return n;
        }

        private int TotalNpc()
        {
            return config.CantQuietos + config.CantCorriendo + config.CantCaminando + config.CantMinis + config.CantMinisDobles * 2;
        }

        private float ValorCampo(string campo)
        {
            switch (campo)
            {
                case "quietos": return config.CantQuietos;
                case "correr": return config.CantCorriendo;
                case "caminar": return config.CantCaminando;
                case "mini": return config.CantMinis;
                case "minidoble": return config.CantMinisDobles;
                case "radio": return config.Radius;
                case "reaparicion": return config.RespawnSeconds;
                case "anillos": return config.Rings;
                case "distancia": return config.MinSpawnDistance;
            }
            return 0f;
        }

        private void AplicarCampo(string campo, float valor)
        {
            switch (campo)
            {
                case "quietos":
                    config.CantQuietos = Mathf.Clamp(Mathf.RoundToInt(valor), 0, 500);
                    break;
                case "correr":
                    config.CantCorriendo = Mathf.Clamp(Mathf.RoundToInt(valor), 0, 500);
                    break;
                case "caminar":
                    config.CantCaminando = Mathf.Clamp(Mathf.RoundToInt(valor), 0, 500);
                    break;
                case "mini":
                    config.CantMinis = Mathf.Max(0, Mathf.RoundToInt(valor));
                    break;
                case "minidoble":
                    config.CantMinisDobles = Mathf.Max(0, Mathf.RoundToInt(valor));
                    break;
                case "radio":
                    config.Radius = Mathf.Clamp(Mathf.Round(valor), 10f, 2000f);
                    break;
                case "reaparicion":
                    config.RespawnSeconds = Mathf.Clamp(Mathf.Round(valor * 10f) / 10f, 1f, 600f);
                    break;
                case "anillos":
                    config.Rings = Mathf.Clamp(Mathf.RoundToInt(valor), 1, 20);
                    break;
                case "distancia":
                    config.MinSpawnDistance = Mathf.Clamp(Mathf.Round(valor), 0f, 500f);
                    break;
                default:
                    return;
            }
            SaveConfig();
        }

        private List<string> NombresConjuntos()
        {
            var lista = new List<string>(outfits.Keys);
            lista.Sort(StringComparer.OrdinalIgnoreCase);
            return lista;
        }

        private void GuardarRopa(BasePlayer jugador, string nombre)
        {
            nombre = (nombre ?? "").Trim();

            if (nombre.Length == 0)
            {
                int n = outfits.Count + 1;
                while (outfits.ContainsKey("Conjunto " + n)) n++;
                nombre = "Conjunto " + n;
            }

            var set = new OutfitSet { Name = nombre };

            foreach (Item item in jugador.inventory.containerWear.itemList)
            {
                set.Items.Add(new ItemData
                {
                    ShortName = item.info.shortname,
                    Skin = item.skin,
                    Amount = item.amount,
                    Container = "wear",
                    Slot = item.position
                });
            }

            // Si tienes un arma o herramienta en la mano, tambien se guarda
            Item activo = jugador.GetActiveItem();
            if (activo != null)
            {
                HeldEntity mano = activo.GetHeldEntity() as HeldEntity;
                if (mano is BaseProjectile || mano is BaseMelee)
                {
                    set.Items.Add(new ItemData
                    {
                        ShortName = activo.info.shortname,
                        Skin = activo.skin,
                        Amount = 1,
                        Container = "held",
                        Slot = 0
                    });
                }
            }

            if (set.Items.Count == 0)
            {
                SendReply(jugador, Prefijo + "No llevas ropa equipada ni arma en la mano para guardar.");
                return;
            }

            outfits[nombre] = set;
            SaveOutfits();
        }

        #endregion

        #region Arena

        private void IniciarArena(BasePlayer jugador)
        {
            if (arenaActiva) return;

            LimpiarRestos();

            centro = jugador.transform.position;
            idJugadorArena = jugador.userID;
            erroresMini = 0;
            errorMostrado = false;
            arenaActiva = true;
            contadorNpc = 0;
            pendientes.Clear();
            ultimoTick = Time.realtimeSinceStartup;

            if (config.RemoveAnimals) EscanearAnimales();

            arenaTimer = timer.Every(0.1f, TickArena);

            // El cuadro de distancia aparece en blanco hasta el primer golpe
            textoMarcador.Clear();
            RefrescarMarcadores();
        }

        private void DetenerArena()
        {
            arenaActiva = false;

            if (arenaTimer != null)
            {
                arenaTimer.Destroy();
                arenaTimer = null;
            }

            foreach (Bot b in new List<Bot>(bots))
            {
                EliminarMini(b);
                LimpiarPasajero(b);
                if (b.Npc != null && !b.Npc.IsDestroyed) b.Npc.Kill();
            }

            foreach (Caida c in new List<Caida>(caidas)) TerminarCaida(c);
            caidas.Clear();

            bots.Clear();
            botNpcs.Clear();
            minis.Clear();
            pendientes.Clear();

            LimpiarEstadosJugadores();
        }

        // Al detener la arena el jugador vuelve a su estado normal:
        //  - inmortalidad, municion infinita y sin desgaste dependen de arenaActiva (ya quedan sin efecto)
        //  - se quita el cuadro de distancia, su vista previa y los datos guardados de la ultima distancia
        private void LimpiarEstadosJugadores()
        {
            foreach (var t in timersMarcador.Values)
                if (t != null && !t.Destroyed) t.Destroy();

            timersMarcador.Clear();
            ultimoMarcador.Clear();
            textoMarcador.Clear();

            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (p == null || !p.IsConnected) continue;
                CuiHelper.DestroyUi(p, UiMarcador);
            }
        }

        private void QuitarBot(Bot b)
        {
            bots.Remove(b);
            botNpcs.Remove(b.Npc);
            EliminarMini(b);
            LimpiarPasajero(b);
            if (b.Npc != null && !b.Npc.IsDestroyed) b.Npc.Kill();
        }

        // Quita al pasajero de un mini de 2 personas (sin cuerpo ni restos)
        private void LimpiarPasajero(Bot b)
        {
            if (b == null || b.Pasajero == null) return;

            botNpcs.Remove(b.Pasajero);
            if (!b.Pasajero.IsDestroyed) b.Pasajero.Kill();
            b.Pasajero = null;
        }

        // ---- Mini sin piloto: cae con fisicas reales ----
        private class Caida
        {
            public BaseVehicle Mini;
            public BasePlayer Pasajero;
            public float Inicio;
            public float Fin;             // limite de tiempo por si nunca llega a pararse
            public Vector3 UltimaPos;
            public float Quieto;          // tiempo seguido sin moverse (ya aterrizo)
            public bool Destruido;        // ya esta condenado: se retira en el siguiente frame
        }

        private readonly List<Caida> caidas = new List<Caida>();

        // Daño del mini que cae:
        //  - el daño "natural" del juego se ignora y se calcula aqui segun la fuerza de cada impacto
        //  - un impacto (o un disparo) que lo destruiria no lo hace explotar: se retira sin restos
        //    y el pasajero que siga dentro muere
        private bool matandoPasajero;

        private object DanoMiniCaida(Caida cd, BaseCombatEntity mini, HitInfo info)
        {
            if (config.BotsImmortal || cd.Destruido) return true;

            // El daño por choque lo genera el propio juego (el mini ya trae el suyo): aqui solo se
            // vigila que, si fuera a destruirse, no explote ni deje restos.
            float dano = info.damageTypes.Total();
            if (dano < mini.health) return null; // lo recibe y sigue cayendo, ya con daño

            // Lo destruiria: se retira sin explosion ni restos
            cd.Destruido = true;
            BasePlayer culpable = info.InitiatorPlayer;
            NextTick(() =>
            {
                if (caidas.Remove(cd)) TerminarCaida(cd, true, culpable);
            });
            return true;
        }

        // El piloto ha muerto: se apaga el motor y se devuelven las fisicas al mini, que sigue
        // con la velocidad que llevaba y cae solo. El pasajero (si vive) cae con el.
        private void IniciarCaida(Bot b)
        {
            BaseVehicle mini = b.Mini;
            minis.Remove(mini);
            b.Mini = null;

            try
            {
                mini.flags &= ~BaseEntity.Flags.On; // motor apagado: las helices se van parando
                mini.SendNetworkUpdate();

                Rigidbody cuerpo = b.Cuerpo != null ? b.Cuerpo : mini.GetComponent<Rigidbody>();
                if (cuerpo != null)
                {
                    cuerpo.isKinematic = false;

                    // Conserva la velocidad que llevaba y un pequeño giro para que no caiga "recto"
                    Vector3 v = b.VelH;
                    v.y = b.VelV;
                    cuerpo.AddForce(v, ForceMode.VelocityChange);
                    cuerpo.AddTorque(new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(-0.8f, 0.8f),
                        UnityEngine.Random.Range(-0.4f, 0.4f)), ForceMode.VelocityChange);
                }
            }
            catch (Exception ex)
            {
                RegistrarError("caida del mini", ex);
            }

            float ahora = Time.realtimeSinceStartup;
            caidas.Add(new Caida
            {
                Mini = mini,
                Pasajero = b.Pasajero,
                Inicio = ahora,
                Fin = ahora + 25f,
                UltimaPos = mini.transform.position
            });
            b.Pasajero = null;
        }

        // Cada tick: mantiene sincronizado el mini que cae y lo retira cuando ya aterrizo
        private void ProcesarCaidas(float ahora)
        {
            for (int i = caidas.Count - 1; i >= 0; i--)
            {
                Caida c = caidas[i];
                if (c.Destruido) continue; // lo retira el NextTick del daño

                bool miniPerdido = c.Mini == null || c.Mini.IsDestroyed;
                bool fin = miniPerdido || ahora >= c.Fin;

                if (!fin)
                {
                    try { c.Mini.SendNetworkUpdate_Position(); }
                    catch (Exception) { }


                    Vector3 p = c.Mini.transform.position;
                    if ((p - c.UltimaPos).sqrMagnitude < 0.0025f) c.Quieto += 0.1f;
                    else
                    {
                        c.Quieto = 0f;
                        c.UltimaPos = p;
                    }

                    if (ahora - c.Inicio > 2f && c.Quieto >= 3f) fin = true;
                }

                if (fin)
                {
                    caidas.RemoveAt(i);
                    TerminarCaida(c, miniPerdido, null);
                }
            }
        }

        // Fin de la caida. Sin "destruido" el mini aterrizo entero (o se agoto el tiempo): se retira sin
        // explosion ni restos y el pasajero desaparece. Con "destruido" el pasajero que siga dentro muere.
        private void TerminarCaida(Caida c, bool destruido = false, BasePlayer culpable = null)
        {
            BasePlayer pas = c.Pasajero;
            c.Pasajero = null;

            if (pas != null && !pas.IsDestroyed)
            {
                if (destruido && !pas.IsDead())
                {
                    matandoPasajero = true;
                    try { pas.Hurt(1000f, Rust.DamageType.Collision, culpable, false); }
                    catch (Exception ex) { RegistrarError("muerte del pasajero", ex); }
                    finally { matandoPasajero = false; }
                }

                // Si por algun motivo sigue vivo, se retira igualmente
                if (!pas.IsDestroyed && !pas.IsDead())
                {
                    botNpcs.Remove(pas);
                    pas.Kill();
                }
            }
            else if (pas != null)
            {
                botNpcs.Remove(pas);
            }

            if (c.Mini != null && !c.Mini.IsDestroyed)
            {
                VaciarCombustible(c.Mini);
                muertes.Add(new KeyValuePair<Vector3, float>(c.Mini.transform.position, Time.realtimeSinceStartup));
                c.Mini.Kill();
            }
            c.Mini = null;
        }

        // Elimina el minihelicoptero sin explosion ni restos
        private void EliminarMini(Bot b)
        {
            if (b == null || b.Mini == null) return;

            minis.Remove(b.Mini);
            if (!b.Mini.IsDestroyed)
            {
                VaciarCombustible(b.Mini); // asi no suelta gasolina al desaparecer
                muertes.Add(new KeyValuePair<Vector3, float>(b.Mini.transform.position, Time.realtimeSinceStartup));
                b.Mini.Kill();
            }
            b.Mini = null;
        }

        private readonly HashSet<string> erroresVistos = new HashSet<string>();
        private readonly System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();
        private int cursorFrame;

        // Cada error distinto se muestra una vez con su traza completa (para poder localizarlo)
        private void RegistrarError(string donde, Exception ex)
        {
            string clave = donde + "|" + ex.GetType().Name + "|" + ex.Message;
            if (erroresVistos.Count < 30 && erroresVistos.Add(clave))
                PrintError("Error en " + donde + ": " + ex);
        }

        private void TickArena()
        {
            if (!arenaActiva) return;

            try
            {
                float ahora = Time.realtimeSinceStartup;
                ultimoTick = ahora;
                contadorTick++;

                ProcesarCaidas(ahora);

                if (!Mantener(ahora)) return;

                if (config.RemoveAnimals && contadorTick % 20 == 1) LimpiarAnimales(); // cada 2 s

                bool calcularLod = contadorTick % 5 == 0;

                for (int i = bots.Count - 1; i >= 0; i--)
                {
                    Bot b = bots[i];

                    if (b.Npc == null || b.Npc.IsDestroyed || b.Npc.IsDead())
                    {
                        bots.RemoveAt(i);
                        if (b.Npc != null) botNpcs.Remove(b.Npc);
                        EliminarMini(b);
                        LimpiarPasajero(b);
                        continue;
                    }

                    if (b.Roto)
                    {
                        QuitarBot(b);
                        continue;
                    }

                    if (calcularLod) b.Lod = EsMini(b.Tipo) ? 1 : NivelLod(b.Npc.transform.position);
                }
            }
            catch (Exception ex)
            {
                RegistrarError("TickArena", ex);
            }
        }

        // El movimiento va en OnFrame para que sea fluido, pero cada NPC solo "piensa" a su ritmo
        // (20 veces/s cerca, menos lejos) y hay un presupuesto de tiempo por frame: si se gasta,
        // el resto sigue en el frame siguiente. Asi el servidor nunca se bloquea por los NPC.
        private void OnFrame(float delta)
        {
            if (!arenaActiva || bots.Count == 0) return;

            try
            {
                float ahora = Time.realtimeSinceStartup;
                contadorFrame++;
                redFrame = 0;

                reloj.Reset();
                reloj.Start();

                int total = bots.Count;
                if (cursorFrame >= total) cursorFrame = 0;

                bool corteDePresupuesto = false;

                for (int k = 0; k < total && k < bots.Count; k++)
                {
                    // El presupuesto se mira ANTES de mover cada NPC (asi nunca se pasa por mucho)
                    if (k > 0 && reloj.Elapsed.TotalMilliseconds > PresupuestoMs)
                    {
                        cursorFrame = (cursorFrame + k) % bots.Count;
                        corteDePresupuesto = true;
                        break;
                    }

                    int i = (cursorFrame + k) % bots.Count;
                    Bot b = bots[i];

                    if (b == null || b.Roto || b.Npc == null || b.Npc.IsDestroyed || b.Npc.IsDead()) continue;
                    if (b.Tipo == TipoBot.Quieto) continue; // los quietos no se mueven

                    // Maximo 20 pasos por segundo y NPC (los de lejos, menos). Si el servidor va justo,
                    // factorCarga alarga todos los intervalos automaticamente.
                    float intervalo = EsMini(b.Tipo) ? 0f : 0.05f * Mathf.Max(1, b.Lod) * factorCarga;
                    float dtBot = ahora - b.UltimoMov;
                    if (dtBot < intervalo) continue;

                    b.UltimoMov = ahora;
                    if (dtBot > 0.25f) dtBot = 0.25f;

                    // Un error en un NPC no debe repetirse en cada frame (eso bloquea el servidor):
                    // se retira ese NPC y se avisa con el detalle
                    try
                    {
                        if (EsMini(b.Tipo)) MoverMini(b, dtBot, i, ahora);
                        else MoverBot(b, dtBot, i, ahora);
                    }
                    catch (Exception ex)
                    {
                        b.Roto = true;
                        RegistrarError("mover NPC (" + b.Tipo + ")", ex);

                        if (EsMini(b.Tipo) && ++erroresMini >= 3 && (config.CantMinis > 0 || config.CantMinisDobles > 0))
                        {
                            config.CantMinis = 0;
                            config.CantMinisDobles = 0;
                            SaveConfig();
                            PrintError("Demasiados errores con los minihelicópteros: se ponen a 0.");
                        }
                    }

                }

                // Carga adaptativa: si se agota el presupuesto se piensa menos a menudo; si sobra, se recupera
                if (corteDePresupuesto) factorCarga = Mathf.Min(factorCarga * 1.25f, 8f);
                else if (reloj.Elapsed.TotalMilliseconds < PresupuestoMs * 0.5) factorCarga = Mathf.Max(1f, factorCarga * 0.98f);
            }
            catch (Exception ex)
            {
                RegistrarError("OnFrame", ex);
            }
        }

        // Cada cuantos "pasos" piensa un NPC segun lo cerca que tenga al jugador mas proximo
        private int NivelLod(Vector3 pos)
        {
            float mejor = float.MaxValue;

            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (p == null || !p.IsConnected || p.IsDead()) continue;
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < mejor) mejor = d;
            }

            if (mejor < 100f * 100f) return 1;  // cerca: 20 veces por segundo
            if (mejor < 200f * 200f) return 2;  // medio
            return 4;                           // lejos
        }

        // Mantiene la cantidad de NPC pedida de cada tipo. Devuelve false si hubo que parar la arena.
        private bool Mantener(float ahora)
        {
            foreach (TipoBot tipo in TodosLosTipos)
            {
                int maximo = Cantidad(tipo);

                // Sobran: se quitan primero los huecos de numero mas alto
                for (int i = pendientes.Count - 1; i >= 0; i--)
                    if (pendientes[i].Tipo == tipo && pendientes[i].Slot >= maximo) pendientes.RemoveAt(i);

                for (int i = bots.Count - 1; i >= 0; i--)
                    if (bots[i].Tipo == tipo && bots[i].Slot >= maximo) QuitarBot(bots[i]);

                // Faltan: se ocupan los huecos libres
                var ocupados = new HashSet<int>();
                foreach (Bot b in bots) if (b.Tipo == tipo) ocupados.Add(b.Slot);
                foreach (Pendiente p in pendientes) if (p.Tipo == tipo) ocupados.Add(p.Slot);

                for (int s = 0; s < maximo; s++)
                {
                    if (!ocupados.Contains(s))
                        pendientes.Add(new Pendiente { Tipo = tipo, Slot = s, Hora = ahora });
                }
            }

            // Se crean pocos por tick para no bloquear el servidor
            int creados = 0;
            for (int i = pendientes.Count - 1; i >= 0 && creados < 3; i--)
            {
                if (pendientes[i].Hora > ahora) continue;

                TipoBot tipo = pendientes[i].Tipo;
                int hueco = pendientes[i].Slot;
                int fallos = pendientes[i].Fallos;
                pendientes.RemoveAt(i);

                if (!CrearBot(tipo, hueco, fallos))
                {
                    DetenerArena();
                    return false;
                }
                creados++;
            }

            return true;
        }

        // Punto (relativo al centro) que le toca a un hueco:
        //  - el radio se reparte en anillos del mismo ancho y cada anillo recibe los mismos NPC
        //    (asi hay NPC cerca y lejos, no solo en el borde)
        //  - dentro de cada anillo los NPC se reparten por sectores de angulo, con un pequeño
        //    desplazamiento al azar para que no parezca un circulo perfecto
        // Cada tipo de NPC se reparte por separado, asi todos los tipos quedan bien repartidos.
        private Vector2 PuntoDeHueco(TipoBot tipo, int hueco)
        {
            int anillos = Mathf.Max(1, config.Rings);
            int total = Mathf.Max(1, Cantidad(tipo));

            int anillo = hueco % anillos;
            int indice = hueco / anillos;
            int enAnillo = Mathf.Max(1, (total - anillo + anillos - 1) / anillos);

            float sector = 360f / enAnillo;
            float baseAngulo = anillo * 137.5f + indice * sector; // cada anillo gira distinto
            float angulo = (baseAngulo + UnityEngine.Random.Range(-0.35f, 0.35f) * sector) * Mathf.Deg2Rad;

            float ancho = config.Radius / anillos;
            float interior = anillo * ancho;
            float radio = Mathf.Max(4f, Mathf.Lerp(interior, interior + ancho, UnityEngine.Random.Range(0.1f, 0.9f)));

            return new Vector2(Mathf.Sin(angulo) * radio, Mathf.Cos(angulo) * radio);
        }

        // true si hay algun jugador a menos de "minimo" metros (en horizontal) de ese punto
        private bool CercaDeJugador(Vector3 p, float minimo)
        {
            if (minimo <= 0f) return false;

            float min2 = minimo * minimo;
            foreach (BasePlayer j in BasePlayer.activePlayerList)
            {
                if (j == null || !j.IsConnected || j.IsDead() || botNpcs.Contains(j)) continue;

                Vector3 d = j.transform.position - p;
                if (d.x * d.x + d.z * d.z < min2) return true;
            }
            return false;
        }

        // Busca un sitio valido dentro del anillo/sector del hueco: sobre el terreno, fuera del agua,
        // sin rocas, arboles ni paredes encima y a la distancia minima de los jugadores.
        // Si su sector no tiene sitio, prueba en cualquier punto libre de la arena.
        private bool BuscarPosicion(TipoBot tipo, int hueco, bool relajarVisibilidad, out Vector3 posicion)
        {
            float minimo = Mathf.Clamp(config.MinSpawnDistance, 0f, config.Radius * 0.9f);

            // Si esta activado, el NPC solo aparece donde el jugador de la arena lo pueda ver (en cualquier
            // direccion: vale con girar). Se prefiere verlo entero; como minimo, la mitad o la cabeza.
            Vector3 ojo = Vector3.zero;
            bool exigirVista = false;
            if (config.SpawnVisible && !EsMini(tipo) && !relajarVisibilidad)
            {
                BasePlayer vigia = BasePlayer.FindByID(idJugadorArena);
                if (vigia != null && vigia.IsConnected && !vigia.IsDead())
                {
                    ojo = vigia.eyes != null ? vigia.eyes.position : vigia.transform.position + Vector3.up * 1.6f;
                    exigirVista = true;
                }
            }

            // Con arenas grandes y terreno montañoso muchos puntos quedan tapados: se prueban mas
            int intentos = exigirVista ? 72 : 24;
            int deAnillo = exigirVista ? 36 : 12;
            int mejorPuntuacion = 0;
            Vector3 mejorPunto = Vector3.zero;

            for (int i = 0; i < intentos; i++)
            {
                Vector2 o = i < deAnillo
                    ? PuntoDeHueco(tipo, hueco)
                    : UnityEngine.Random.insideUnitCircle * config.Radius;

                Vector3 p = new Vector3(centro.x + o.x, 0f, centro.z + o.y);

                // Lo mas barato primero: nada de aparecer cerca de un jugador
                if (CercaDeJugador(p, minimo)) continue;

                float altura = TerrainMeta.HeightMap != null ? TerrainMeta.HeightMap.GetHeight(p) : centro.y;
                if (altura < 0.6f) continue; // agua

                // Raycast vertical desde el cielo para sacar la altura exacta del suelo
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(p.x, altura + 100f, p.z), Vector3.down, out hit, 200f,
                    maskSuelo, QueryTriggerInteraction.Ignore))
                    continue;

                // Si el "suelo" esta muy por encima del terreno es un tejado o una roca grande
                if (hit.point.y - altura > 1.0f) continue;

                Vector3 punto = hit.point + Vector3.up * 0.1f;

                // Espacio libre: nada de aparecer dentro de un arbol, una roca o una pared
                if (Physics.CheckSphere(punto + Vector3.up * 1.0f, 0.5f, maskObstaculos,
                    QueryTriggerInteraction.Ignore))
                    continue;

                if (exigirVista)
                {
                    int vista = PartesVisibles(ojo, punto);
                    if (vista == 0) continue;

                    // Prioridad: 1) entero y sin arboles en medio, 2) entero entre arboles,
                    // 3) a medias sin arboles, 4) a medias entre arboles, ... (7 = el mejor)
                    bool limpio = !HayArbolesEnMedio(ojo, punto);
                    int puntuacion = vista * 2 + (limpio ? 1 : 0);

                    if (puntuacion >= 7)
                    {
                        posicion = punto;
                        return true;
                    }

                    if (puntuacion > mejorPuntuacion)
                    {
                        mejorPuntuacion = puntuacion;
                        mejorPunto = punto;
                    }
                    continue; // se sigue buscando uno mejor
                }

                posicion = punto;
                return true;
            }

            // Ninguno era perfecto: vale el mejor que se encontro (como minimo, se veia la cabeza)
            if (exigirVista && mejorPuntuacion > 0)
            {
                posicion = mejorPunto;
                return true;
            }

            posicion = centro + Vector3.up * 0.5f;
            return false;
        }

        // true si hay troncos junto al NPC o pegados a la linea de vision. El servidor no "ve" las hojas
        // (solo el tronco tiene colision), asi que se evitan los sitios donde las copas probablemente tapan.
        private bool HayArbolesEnMedio(Vector3 ojo, Vector3 punto)
        {
            // Arboles alrededor del NPC: sus hojas lo taparian aunque la linea este libre
            if (Physics.CheckSphere(punto + Vector3.up * 2f, 4f, maskArboles, QueryTriggerInteraction.Ignore))
                return true;

            // Arboles cerca de la linea que va del ojo al pecho del NPC
            Vector3 dir = (punto + Vector3.up * 1.0f) - ojo;
            float dist = dir.magnitude;
            if (dist < 3f) return false;

            return Physics.SphereCast(new Ray(ojo, dir / dist), 2f, dist - 1f, maskArboles,
                QueryTriggerInteraction.Ignore);
        }

        // Cuantas partes del cuerpo (cabeza, pecho, pies) se ven desde el ojo del jugador: 0 a 3
        private int PartesVisibles(Vector3 ojo, Vector3 punto)
        {
            int visibles = 0;
            float[] alturas = { 1.6f, 1.0f, 0.3f };

            for (int i = 0; i < alturas.Length; i++)
            {
                Vector3 parte = punto + Vector3.up * alturas[i];
                if (!Physics.Linecast(ojo, parte, maskVuelo, QueryTriggerInteraction.Ignore)) visibles++;
            }

            return visibles;
        }

        // Elimina NPC de la arena que hayan quedado de una carga anterior del plugin
        private void LimpiarRestos()
        {
            foreach (BasePlayer p in new List<BasePlayer>(BasePlayer.allPlayerList))
            {
                if (p == null || p.IsDestroyed || botNpcs.Contains(p)) continue;
                if (!p.IsConnected && p.displayName != null && p.displayName.StartsWith("Arena NPC")) p.Kill();
            }
        }

        private bool CrearBot(TipoBot tipo, int hueco, int fallos = 0)
        {
            Vector3 pos;
            if (!BuscarPosicion(tipo, hueco, fallos >= 5, out pos))
            {
                // Sin sitio libre en su sector: se reintenta en un segundo
                pendientes.Add(new Pendiente
                {
                    Tipo = tipo, Slot = hueco, Fallos = fallos + 1,
                    Hora = Time.realtimeSinceStartup + 1f
                });
                return true;
            }

            Quaternion rotInicial = Quaternion.identity;
            if (tipo == TipoBot.Quieto)
            {
                BasePlayer objetivo = BasePlayer.FindByID(idJugadorArena);
                if (objetivo == null || !objetivo.IsConnected) objetivo = JugadorMasCercano(pos);
                if (objetivo != null)
                {
                    Vector3 hacia = objetivo.transform.position - pos;
                    hacia.y = 0f;
                    if (hacia.sqrMagnitude > 0.01f) rotInicial = Quaternion.LookRotation(hacia.normalized);
                }
            }

            BaseEntity entidad = GameManager.server.CreateEntity(PrefabJugador, pos, rotInicial);
            BasePlayer npc = entidad as BasePlayer;

            if (npc == null)
            {
                PrintError("No se pudo crear el NPC. Comprueba que la ruta del prefab sea válida.");
                if (entidad != null) entidad.Kill();
                return false;
            }

            // Jugador normal sin conexion (como HumanNPC): id propio y posicion sincronizada por el juego
            npc.enableSaving = false;
            // Id con forma de Steam ID (los ids pequeños los trata el juego como bots y los registra dos veces)
            do { idUnico++; } while (BasePlayer.FindByID(IdBotBase + idUnico) != null);
            npc.userID = IdBotBase + idUnico;
            npc.UserIDString = npc.userID.ToString();
            npc.syncPosition = true;

            // Ya desde antes de aparecer mira al jugador (cuerpo, cabeza y direccion del modelo),
            // asi el estado interno del juego coincide con lo que ven los clientes.
            if (tipo == TipoBot.Quieto)
            {
                Vector3 angIni = rotInicial.eulerAngles;
                npc.viewAngles = angIni;
                npc.transform.rotation = rotInicial;
                if (npc.eyes != null) npc.eyes.rotation = rotInicial;
            }

            try
            {
                npc.Spawn();
            }
            catch (Exception ex)
            {
                RegistrarError("crear NPC", ex);
                if (!npc.IsDestroyed) npc.Kill();
                return true; // se reintenta en el siguiente tick con otro id
            }

            if (npc.IsSleeping()) npc.EndSleeping();

            contadorNpc++;
            npc.displayName = "Arena NPC #" + contadorNpc;

            // Vida maxima
            npc.InitializeHealth(100f, 100f);

            Item arma = EquiparConjunto(npc, !EsMini(tipo)); // el piloto va sentado, sin arma en la mano

            // El arma ya aparece en la mano
            if (arma != null) npc.UpdateActiveItem(arma.uid);
            npc.SendNetworkUpdateImmediate();

            // Respaldo: si el juego aun no tenia lista el arma, se vuelve a intentar un instante despues
            if (arma != null)
            {
                timer.Once(0.3f, () =>
                {
                    if (npc != null && !npc.IsDestroyed && !npc.IsDead() && arma != null &&
                        npc.GetActiveItem() != arma)
                        npc.UpdateActiveItem(arma.uid);
                });
            }

            var bot = new Bot
            {
                Tipo = tipo,
                Npc = npc,
                Slot = hueco,
                Home = pos,
                SondeoEspera = UnityEngine.Random.Range(0f, 0.2f),
                UltimoMov = Time.realtimeSinceStartup,
                PosGrupo = pos,
                AltVuelo = UnityEngine.Random.Range(28f, 48f)
            };
            bot.Mira = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
            bots.Add(bot);
            botNpcs.Add(npc);

            if (EsMini(tipo) && !MontarMini(bot, pos))
            {
                // No se pudo: se quita este NPC y se desactiva el tipo para no repetir el error
                bots.Remove(bot);
                botNpcs.Remove(npc);
                if (!npc.IsDestroyed) npc.Kill();

                PrintError("No se pudo crear el minihelicóptero con piloto. Se pone la cantidad de este tipo a 0.");
                if (tipo == TipoBot.Mini) config.CantMinis = 0;
                else config.CantMinisDobles = 0;
                SaveConfig();
                return true;
            }

            if (tipo == TipoBot.MiniDoble) CrearPasajero(bot, pos);

            if (tipo == TipoBot.Quieto)
            {
                // Se queda parado mirando al jugador desde el primer momento.
                // Se repite una vez a 1 s por si el juego reinicia la rotacion al aparecer.
                ModelState reposo = npc.modelState;
                if (reposo != null)
                {
                    reposo.sprinting = false;
                    reposo.ducked = false;
                    reposo.onground = true;
                }

                MirarAlJugador(npc);

                // Una vez al aparecer y otra cuando el cliente ya lo tiene (con el arma en la mano).
                // Es la misma direccion que la de aparicion, asi que no hay saltos al recibir dano.
                timer.Once(0.4f, () => MirarAlJugador(npc));
            }

            return true;
        }

        private bool avisoPasajero;

        // Segunda persona de un mini de 2 personas: aparece sentada en el asiento del pasajero
        private void CrearPasajero(Bot bot, Vector3 pos)
        {
            if (bot.Mini == null || bot.Mini.IsDestroyed) return;

            BasePlayer pas = null;
            try
            {
                BaseEntity entidad = GameManager.server.CreateEntity(PrefabJugador, pos, Quaternion.identity);
                pas = entidad as BasePlayer;
                if (pas == null)
                {
                    if (entidad != null) entidad.Kill();
                    return;
                }

                pas.enableSaving = false;
                do { idUnico++; } while (BasePlayer.FindByID(IdBotBase + idUnico) != null);
                pas.userID = IdBotBase + idUnico;
                pas.UserIDString = pas.userID.ToString();
                pas.syncPosition = true;
                pas.Spawn();

                if (pas.IsSleeping()) pas.EndSleeping();

                contadorNpc++;
                pas.displayName = "Arena NPC #" + contadorNpc;
                pas.InitializeHealth(100f, 100f);
                botNpcs.Add(pas);

                Item arma = EquiparConjunto(pas, true);
                if (arma != null) pas.UpdateActiveItem(arma.uid);
                pas.SendNetworkUpdateImmediate();

                // Asiento que no es el del piloto
                if (bot.Mini.mountPoints != null)
                {
                    foreach (var punto in bot.Mini.mountPoints)
                    {
                        if (punto != null && !punto.isDriver && punto.mountable != null &&
                            punto.mountable.GetMounted() == null)
                        {
                            punto.mountable.MountPlayer(pas);
                            break;
                        }
                    }
                }

                if (pas.GetMounted() == null) bot.Mini.AttemptMount(pas, false);

                if (pas.GetMounted() == null)
                {
                    botNpcs.Remove(pas);
                    pas.Kill();
                    if (!avisoPasajero)
                    {
                        avisoPasajero = true;
                        PrintError("El pasajero no pudo sentarse en el minihelicóptero: este mini volará solo con piloto.");
                    }
                    return;
                }

                bot.Pasajero = pas;
            }
            catch (Exception ex)
            {
                RegistrarError("crear pasajero", ex);
                if (pas != null)
                {
                    botNpcs.Remove(pas);
                    if (!pas.IsDestroyed) pas.Kill();
                }
            }
        }

        private BasePlayer JugadorMasCercano(Vector3 pos)
        {
            BasePlayer mejor = null;
            float mejorDist = float.MaxValue;

            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (p == null || !p.IsConnected || p.IsDead()) continue;
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < mejorDist)
                {
                    mejorDist = d;
                    mejor = p;
                }
            }

            return mejor;
        }

        // Gira al NPC hacia quien inicio la arena (o el jugador mas cercano si ya no esta)
        private void MirarAlJugador(BasePlayer npc)
        {
            if (npc == null || npc.IsDestroyed || npc.IsDead()) return;

            BasePlayer objetivo = BasePlayer.FindByID(idJugadorArena);
            if (objetivo == null || !objetivo.IsConnected || objetivo.IsDead())
                objetivo = JugadorMasCercano(npc.transform.position);
            if (objetivo == null) return;

            Vector3 d = objetivo.transform.position - npc.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;

            Quaternion rot = Quaternion.LookRotation(d.normalized);
            Quaternion cuerpo = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);

            npc.viewAngles = rot.eulerAngles;
            npc.transform.rotation = cuerpo;
            if (npc.eyes != null) npc.eyes.rotation = cuerpo; // sin esto el juego mantiene la mirada anterior

            // Los clientes orientan el modelo con lookDir: sin esto solo giraba al recibir un disparo
            if (npc.modelState != null) npc.modelState.lookDir = d.normalized;

            // El giro del CUERPO lo toma el cliente del mensaje de posicion (con rotacion), no del
            // estado completo: por eso solo giraba al recibir dano. Se envia expresamente.
            npc.MovePosition(npc.transform.position);
            npc.SendNetworkUpdateImmediate();
            npc.SendNetworkUpdate_Position();
        }

        // Crea un minihelicoptero en el aire y sienta al NPC como piloto.
        // El mini no se mueve con fisicas: lo mueve el plugin (como al resto de NPC).
        private bool MontarMini(Bot bot, Vector3 pos)
        {
            BaseVehicle mini = null;

            try
            {
                float giroInicial = UnityEngine.Random.Range(0f, 360f);
                Quaternion rot = Quaternion.Euler(0f, giroInicial, 0f);
                mini = GameManager.server.CreateEntity(PrefabMini, pos + Vector3.up * AlturaMini, rot) as BaseVehicle;

                if (mini == null)
                {
                    PrintError("No se pudo crear el minihelicóptero. Comprueba la ruta del prefab.");
                    return false;
                }

                mini.enableSaving = false;
                mini.Spawn();
                bot.Giro = giroInicial;
                bot.Vel = 0f;

                // Sin fisicas: no cae ni choca, lo coloca el plugin cada frame
                Rigidbody cuerpo = mini.GetComponent<Rigidbody>();
                if (cuerpo != null) cuerpo.isKinematic = true;
                bot.Cuerpo = cuerpo;

                // Se sienta en el asiento del conductor (no en el primero que encuentre)
                try
                {
                    if (mini.mountPoints != null)
                    {
                        foreach (var punto in mini.mountPoints)
                        {
                            if (punto != null && punto.isDriver && punto.mountable != null)
                            {
                                punto.mountable.MountPlayer(bot.Npc);
                                break;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // si falla se prueba el metodo general
                }

                if (bot.Npc.GetMounted() == null) mini.AttemptMount(bot.Npc, false);

                if (bot.Npc.GetMounted() == null)
                {
                    PrintError("El NPC no pudo sentarse en el minihelicóptero.");
                    mini.Kill();
                    return false;
                }
            }
            catch (Exception ex)
            {
                PrintError("Error al crear el minihelicóptero: " + ex.Message);
                if (mini != null && !mini.IsDestroyed) mini.Kill();
                return false;
            }

            bot.Mini = mini;
            bot.Home = mini.transform.position;
            bot.AltObjetivo = mini.transform.position.y;
            bot.AltSuave = mini.transform.position.y;
            bot.VelH = Vector3.zero;
            bot.Fase = UnityEngine.Random.Range(0f, 100f);
            bot.SondeoEspera = 0f;
            MantenerMotor(mini); // enciende el motor: las helices giran
            bot.UltimaMotor = Time.realtimeSinceStartup + UnityEngine.Random.Range(0f, 4f); // revisiones desfasadas entre minis
            bot.PosGrupo = mini.transform.position;
            minis[mini] = bot;
            return true;
        }

        // Devuelve el arma que lleva el conjunto (si tiene), para sacarla en la mano
        private Item EquiparConjunto(BasePlayer npc, bool conArma = true)
        {
            if (npc == null || npc.inventory == null) return null;

            npc.inventory.containerWear.Clear();

            if (outfits.Count == 0)
            {
                // Sin conjuntos guardados: ropa basica para que no vayan desnudos
                string[] basica = { "hoodie", "pants", "shoes.boots" };
                foreach (string nombre in basica)
                {
                    Item prenda = ItemManager.CreateByName(nombre, 1, 0UL);
                    if (prenda != null) prenda.MoveToContainer(npc.inventory.containerWear);
                }
                return null;
            }

            var disponibles = new List<OutfitSet>(outfits.Values);
            OutfitSet elegido = disponibles[UnityEngine.Random.Range(0, disponibles.Count)];

            Item arma = null;

            foreach (ItemData data in elegido.Items)
            {
                if (data.Container != "wear" && data.Container != "held") continue;
                if (data.Container == "held" && !conArma) continue; // sentado pilotando: sin arma

                Item item = ItemManager.CreateByName(data.ShortName, data.Amount, data.Skin);
                if (item == null) continue;

                if (data.Container == "held")
                {
                    npc.inventory.containerBelt.Clear();
                    if (item.MoveToContainer(npc.inventory.containerBelt)) arma = item;
                }
                else
                {
                    item.MoveToContainer(npc.inventory.containerWear);
                }
            }

            return arma;
        }

        #endregion

        #region Movimiento de los NPC

        private const float PresupuestoMs = 2f; // tiempo maximo por frame que se gasta moviendo NPC
        private const int MaxRedPorFrame = 2;   // actualizaciones de red (caras) permitidas por frame
        private float factorCarga = 1f;         // 1 = normal; mas alto = los NPC piensan menos veces
        private int redFrame;

        private void ElegirMeta(Bot b, Vector3 pos)
        {
            b.Lado = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            b.MetaHasta = Time.time + UnityEngine.Random.Range(15f, 40f);
            b.SondeoEspera = 0f;
            b.TieneMeta = true;

            float patrulla = Mathf.Clamp(config.Radius / Mathf.Max(1, config.Rings) * 1.5f, 15f, 60f);
            float lejos = config.RoamFullArena ? 400f : 36f; // distancia minima al punto elegido (al cuadrado)

            for (int i = 0; i < 10; i++)
            {
                Vector3 p;

                if (config.RoamFullArena)
                {
                    // Libertad total: cualquier punto de la arena
                    Vector2 r = UnityEngine.Random.insideUnitCircle * config.Radius;
                    p = new Vector3(centro.x + r.x, 0f, centro.z + r.y);
                }
                else
                {
                    // Patrulla solo por la zona donde aparecio
                    Vector2 r = UnityEngine.Random.insideUnitCircle * patrulla;
                    p = new Vector3(b.Home.x + r.x, 0f, b.Home.z + r.y);
                    if ((new Vector2(p.x - centro.x, p.z - centro.z)).sqrMagnitude > config.Radius * config.Radius) continue;
                }

                if (TerrainMeta.HeightMap != null && TerrainMeta.HeightMap.GetHeight(p) < 0.6f) continue; // agua
                if ((new Vector2(p.x - pos.x, p.z - pos.z)).sqrMagnitude < lejos) continue; // muy cerca

                b.Meta = p;
                return;
            }

            // Sin sitio bueno: una meta cercana al azar
            Vector2 q = UnityEngine.Random.insideUnitCircle.normalized * 15f;
            b.Meta = new Vector3(pos.x + q.x, 0f, pos.z + q.y);
        }

        private float ultimoAlto; // altura del ultimo obstaculo saltable medido por Sondear

        // Impulso justo para pasar: los pies solo tienen que subir hasta ~0.3 m por debajo del borde
        private float ImpulsoPara(float alto)
        {
            float h = Mathf.Clamp(alto - 0.3f, 0.45f, 1.3f);
            return Mathf.Sqrt(2f * Gravedad * h);
        }

        private float AlturaSuelo(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, p.y + 1.2f, p.z), Vector3.down, out hit, 100f,
                maskSuelo, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return TerrainMeta.HeightMap != null ? TerrainMeta.HeightMap.GetHeight(p) : p.y;
        }

        // El cuerpo del NPC es una capsula: la parte baja queda a 0.45 m del suelo, asi que los
        // escalones pequeños se suben andando y todo lo demas cuenta como obstaculo.
        private bool Bloqueado(Vector3 pos, Vector3 mov, out RaycastHit hit)
        {
            float dist = mov.magnitude;
            if (dist < 0.0001f)
            {
                hit = default(RaycastHit);
                return false;
            }

            return Physics.CapsuleCast(pos + Vector3.up * 0.75f, pos + Vector3.up * 1.5f, 0.3f, mov / dist,
                out hit, dist + 0.1f, maskObstaculos, QueryTriggerInteraction.Ignore);
        }

        // 0 = camino libre, 1 = obstaculo que se puede saltar, 2 = bloqueado
        private int Sondear(Vector3 pos, Vector3 dir, float alcance)
        {
            // Agua delante
            Vector3 delante = pos + dir * alcance;
            if (TerrainMeta.HeightMap != null && TerrainMeta.HeightMap.GetHeight(delante) < 0.3f) return 2;

            RaycastHit hit;

            // Todo el cuerpo (de las rodillas a la cabeza): paredes, vallas, rocas, arboles...
            if (!Physics.CapsuleCast(pos + Vector3.up * 0.7f, pos + Vector3.up * 1.5f, 0.25f, dir, out hit, alcance,
                maskObstaculos, QueryTriggerInteraction.Ignore))
                return 0;

            // Hay algo delante: se mide cuanto de alto es
            Vector3 arriba = hit.point + dir * 0.3f;
            arriba.y = pos.y + 2.4f;

            RaycastHit techo;
            if (!Physics.Raycast(arriba, Vector3.down, out techo, 2.4f, maskObstaculos,
                QueryTriggerInteraction.Ignore))
                return 2; // muy alto o muy fino para pasar por encima

            float alto = techo.point.y - pos.y;

            if (alto < 0.45f) return 0;
            if (alto > AlturaMaxSalto) return 2;

            // Que haya sitio libre por encima para pasar
            if (Physics.Raycast(pos + Vector3.up * (alto + 1.0f), dir, hit.distance + 1f, maskObstaculos,
                QueryTriggerInteraction.Ignore))
                return 2;

            // Se salta cuando esta cerca; si esta lejos se sigue corriendo
            ultimoAlto = alto;
            return hit.distance <= 1.2f ? 1 : 0;
        }

        private Vector3 Esquivar(Vector3 pos, Vector3 dir, float lado, float alcance, out bool salto)
        {
            salto = false;

            int tipo = Sondear(pos, dir, alcance);
            if (tipo == 0) return dir;
            if (tipo == 1)
            {
                salto = true;
                return dir;
            }

            // Bloqueado: se prueba a rodear hacia un lado y hacia el otro
            foreach (float angulo in AngulosEsquivar)
            {
                for (int s = 0; s < 2; s++)
                {
                    float giro = (s == 0 ? lado : -lado) * angulo;
                    Vector3 d = Quaternion.Euler(0f, giro, 0f) * dir;

                    int t = Sondear(pos, d, alcance);
                    if (t == 0) return d;
                    if (t == 1)
                    {
                        salto = true;
                        return d;
                    }
                }
            }

            return -dir; // atrapado: da media vuelta
        }

        private void MoverBot(Bot b, float dt, int indice, float ahora)
        {
            BasePlayer npc = b.Npc;
            Vector3 pos = npc.transform.position;

            b.SaltoEspera -= dt;

            // Si se ha salido de la arena, vuelve hacia dentro
            float dx = pos.x - centro.x, dz = pos.z - centro.z;
            float limite = config.Radius + 5f;
            if (dx * dx + dz * dz > limite * limite)
            {
                b.Meta = new Vector3(centro.x + dx * 0.5f, 0f, centro.z + dz * 0.5f);
                b.TieneMeta = true;
                b.MetaHasta = Time.time + 10f;
            }

            Vector3 haciaMeta = new Vector3(b.Meta.x - pos.x, 0f, b.Meta.z - pos.z);
            if (!b.TieneMeta || haciaMeta.sqrMagnitude < 6f || Time.time > b.MetaHasta)
            {
                ElegirMeta(b, pos);
                haciaMeta = new Vector3(b.Meta.x - pos.x, 0f, b.Meta.z - pos.z);
            }

            if (haciaMeta.sqrMagnitude < 0.01f) return;

            Vector3 dir = haciaMeta.normalized;
            float vel = b.Tipo == TipoBot.Caminar ? config.WalkSpeed : config.RunSpeed;
            float alcance = Mathf.Max(1.3f, vel * 0.45f); // corriendo mira mas lejos

            // Se sondean los obstaculos 5 veces por segundo (o ya, si acaba de chocar)
            b.SondeoEspera -= dt;
            if (b.SondeoEspera <= 0f)
            {
                b.SondeoEspera = 0.2f;

                // Si esta rodeando un obstaculo y el rumbo sigue libre, mantiene el rodeo
                if (Time.time < b.EsquivarHasta && Sondear(pos, b.DirEsq, alcance) == 0)
                {
                    // sin cambios
                }
                else
                {
                    bool s;
                    Vector3 d = Esquivar(pos, dir, b.Lado, alcance, out s);
                    b.DirEsq = d;
                    b.SaltoEsq = s;
                    if (s) b.AltoSalto = ultimoAlto;
                    if (Vector3.Dot(d, dir) < 0.99f) b.EsquivarHasta = Time.time + 0.6f;
                }
            }
            dir = b.DirEsq;

            if (b.SaltoEsq && !b.EnAire && b.SaltoEspera <= 0f)
            {
                b.EnAire = true;
                b.VelY = ImpulsoPara(b.AltoSalto);
                b.SaltoEspera = 0.6f;
            }
            b.SaltoEsq = false;

            // El giro es suave y el NPC avanza hacia donde mira: sin cambios bruscos de direccion
            Vector3 mira = Vector3.Slerp(b.Mira, dir, Mathf.Clamp01(dt * 8f));
            if (mira.sqrMagnitude < 0.001f) mira = dir;
            b.Mira = mira.normalized;

            // Movimiento horizontal: nunca atraviesa nada. Si choca, resbala por la pared
            Vector3 mov = b.Mira * vel * dt;
            RaycastHit golpe;
            if (Bloqueado(pos, mov, out golpe))
            {
                Vector3 n = golpe.normal;
                n.y = 0f;

                bool resbala = false;
                if (n.sqrMagnitude > 0.0001f)
                {
                    Vector3 lateral = Vector3.ProjectOnPlane(mov, n.normalized);
                    RaycastHit golpe2;
                    if (lateral.sqrMagnitude > 0.00001f && !Bloqueado(pos, lateral, out golpe2))
                    {
                        mov = lateral;
                        resbala = true;
                    }
                }

                if (!resbala) mov = Vector3.zero;

                // Que vuelva a mirar el camino ya mismo: salta si se puede o rodea si no
                b.SondeoEspera = 0f;
                b.EsquivarHasta = 0f;
            }

            Vector3 nueva = pos + mov;
            float suelo = AlturaSuelo(nueva);

            if (b.EnAire)
            {
                b.VelY -= Gravedad * dt;
                nueva.y = pos.y + b.VelY * dt;

                if (b.VelY <= 0f && nueva.y <= suelo)
                {
                    nueva.y = suelo;
                    b.EnAire = false;
                    b.VelY = 0f;
                }
            }
            else
            {
                float subida = suelo - pos.y;

                if (subida > 0.6f)
                {
                    if (subida <= AlturaMaxSalto && b.SaltoEspera <= 0f)
                    {
                        // Escalon alto: salta para subirlo (no avanza este paso)
                        b.EnAire = true;
                        b.VelY = ImpulsoPara(subida);
                        b.SaltoEspera = 0.6f;
                        nueva = pos;
                    }
                    else
                    {
                        // Demasiado alto: busca otra meta
                        b.TieneMeta = false;
                        return;
                    }
                }
                else if (pos.y - suelo > 0.5f)
                {
                    // Se acaba el suelo: empieza a caer
                    b.EnAire = true;
                    b.VelY = 0f;
                    nueva.y = pos.y;
                }
                else
                {
                    // Suaviza el cambio de altura para que no vibre al subir o bajar relieve
                    nueva.y = Mathf.Abs(suelo - pos.y) < 0.02f
                        ? suelo
                        : Mathf.Lerp(pos.y, suelo, Mathf.Clamp01(dt * 15f));
                }
            }

            // Si se queda atascado, cambia de meta (y de lado preferido para rodear)
            float movido = new Vector2(nueva.x - pos.x, nueva.z - pos.z).magnitude;
            if (movido < vel * dt * 0.3f && !b.EnAire) b.Atasco += dt;
            else b.Atasco = 0f;

            if (b.Atasco > 1.2f)
            {
                b.Atasco = 0f;
                b.Lado = -b.Lado;
                b.TieneMeta = false;
            }

            npc.MovePosition(nueva);

            // Hacia donde mira
            if (npc.modelState != null) npc.modelState.lookDir = b.Mira;
            Quaternion rot = Quaternion.LookRotation(b.Mira);
            npc.viewAngles = rot.eulerAngles;
            npc.transform.rotation = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);

            // Animacion: siempre corriendo, y en el aire mientras salta
            ModelState estado = npc.modelState;
            bool cambioAnim = false;
            if (estado != null)
            {
                bool enSuelo = !b.EnAire;
                cambioAnim = estado.onground != enSuelo;
                estado.sprinting = b.Tipo != TipoBot.Caminar;
                estado.ducked = false;
                estado.onground = enSuelo;
            }

            // Red: la posicion la sincroniza el propio juego (syncPosition). El estado completo
            // (animacion) es lo mas caro: solo se envia si cambia (salto/aterrizaje) o cada ~1.5 s,
            // y nunca mas de MaxRedPorFrame por frame (el resto espera al siguiente).
            if ((cambioAnim || ahora - b.UltimaCompleta >= 1.5f) && redFrame < MaxRedPorFrame)
            {
                b.UltimaCompleta = ahora;
                npc.SendNetworkUpdate();
                redFrame++;
            }

            // Cambiar de grupo de red tambien es caro: solo cuando se ha alejado bastante (16 m)
            if ((nueva - b.PosGrupo).sqrMagnitude > 256f && redFrame < MaxRedPorFrame)
            {
                b.PosGrupo = nueva;
                npc.UpdateNetworkGroup();
                redFrame++;
            }
        }

        // ---------------- Minihelicoptero ----------------

        private void ElegirMetaMini(Bot b, Vector3 pos)
        {
            b.TieneMeta = true;
            b.MetaHasta = Time.time + UnityEngine.Random.Range(25f, 60f);
            b.Lado = UnityEngine.Random.value < 0.5f ? 1f : -1f;

            float patrulla = Mathf.Clamp(config.Radius / Mathf.Max(1, config.Rings) * 3f, 40f, 150f);

            for (int i = 0; i < 8; i++)
            {
                Vector3 p;

                if (config.RoamFullArena)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * config.Radius;
                    p = new Vector3(centro.x + r.x, 0f, centro.z + r.y);
                }
                else
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * patrulla;
                    p = new Vector3(b.Home.x + r.x, 0f, b.Home.z + r.y);
                    if ((new Vector2(p.x - centro.x, p.z - centro.z)).sqrMagnitude > config.Radius * config.Radius) continue;
                }

                if ((new Vector2(p.x - pos.x, p.z - pos.z)).sqrMagnitude < 3600f) continue; // a menos de 60 m

                b.Meta = p;
                return;
            }

            b.Meta = new Vector3(centro.x, 0f, centro.z);
        }

        // Altura maxima del terreno aqui y a lo largo del rumbo (para subir antes de una montaña)
        private float TerrenoMax(Vector3 pos, Vector3 dir, float alcance)
        {
            if (TerrainMeta.HeightMap == null) return 0f;

            float m = TerrainMeta.HeightMap.GetHeight(pos);
            for (int i = 1; i <= 4; i++)
            {
                float h = TerrainMeta.HeightMap.GetHeight(pos + dir * (alcance * i / 4f));
                if (h > m) m = h;
            }
            return m;
        }

        // Mira el camino del helicoptero: terreno, edificios, torres y arboles.
        // Decide si sube, frena o gira para esquivar.
        private void SondearVuelo(Bot b, Vector3 pos, float ahora)
        {
            float alcance = Mathf.Max(45f, b.Vel * 3.5f);
            Vector3 fwd = Quaternion.Euler(0f, b.Giro, 0f) * Vector3.forward;

            b.AltObjetivo = TerrenoMax(pos, fwd, alcance) + b.AltVuelo;
            b.Frenar = false;

            RaycastHit hit;
            if (Physics.SphereCast(pos, 3.5f, fwd, out hit, alcance, maskVuelo, QueryTriggerInteraction.Ignore))
            {
                b.Frenar = true;

                // 1) Por encima, si el obstaculo es bajo
                if (!Physics.SphereCast(pos + Vector3.up * 25f, 3.5f, fwd, out hit, alcance, maskVuelo,
                    QueryTriggerInteraction.Ignore))
                {
                    b.AltObjetivo = Mathf.Max(b.AltObjetivo, pos.y + 25f);
                }

                // 2) Girando hacia un lado libre
                bool encontrado = false;
                for (int a = 0; a < AngulosVuelo.Length && !encontrado; a++)
                {
                    for (int s = 0; s < 2 && !encontrado; s++)
                    {
                        float signo = s == 0 ? b.Lado : -b.Lado;
                        Vector3 d = Quaternion.Euler(0f, b.Giro + signo * AngulosVuelo[a], 0f) * Vector3.forward;

                        if (!Physics.SphereCast(pos, 3.5f, d, out hit, alcance * 0.8f, maskVuelo,
                            QueryTriggerInteraction.Ignore))
                        {
                            b.Desvio = signo * AngulosVuelo[a];
                            b.DesvioHasta = ahora + 1.5f; // mantiene el rodeo para no zigzaguear
                            encontrado = true;
                        }
                    }
                }
            }
            else if (ahora > b.DesvioHasta)
            {
                b.Desvio = 0f;
            }
        }

        // Deposito de gasolina del mini (por reflexion, para no depender de la version del juego)
        private System.Reflection.MethodInfo miSistemaComb;
        private System.Reflection.MethodInfo miContenedorComb;
        private bool combBuscado;

        private StorageContainer ContenedorCombustible(BaseVehicle mini)
        {
            try
            {
                // La reflexion se resuelve una sola vez (antes se repetia por cada mini y cada segundo)
                if (!combBuscado)
                {
                    combBuscado = true;
                    miSistemaComb = mini.GetType().GetMethod("GetFuelSystem");
                }
                if (miSistemaComb == null) return null;

                object sistema = miSistemaComb.Invoke(mini, null);
                if (sistema == null) return null;

                if (miContenedorComb == null) miContenedorComb = sistema.GetType().GetMethod("GetFuelContainer");
                return miContenedorComb != null ? miContenedorComb.Invoke(sistema, null) as StorageContainer : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Mantiene un poco de gasolina en el deposito del mini
        private void PonerCombustible(BaseVehicle mini)
        {
            try
            {
                StorageContainer cont = ContenedorCombustible(mini);
                if (cont == null || cont.inventory == null) return;

                int cantidad = 0;
                foreach (Item it in cont.inventory.itemList) cantidad += it.amount;
                if (cantidad >= 20) return;

                Item gasolina = ItemManager.CreateByName("lowgradefuel", 50);
                if (gasolina != null && !gasolina.MoveToContainer(cont.inventory)) gasolina.Remove();
            }
            catch (Exception)
            {
                // si no se puede, el mini sigue volando igualmente
            }
        }

        private void VaciarCombustible(BaseVehicle mini)
        {
            try
            {
                StorageContainer cont = ContenedorCombustible(mini);
                if (cont == null || cont.inventory == null) return;

                foreach (Item it in new List<Item>(cont.inventory.itemList)) it.Remove();
                ItemManager.DoRemoves();
            }
            catch (Exception)
            {
                // sin deposito accesible no hay nada que vaciar
            }
        }

        // Mantiene el motor encendido: asi giran las helices
        private void MantenerMotor(BaseVehicle mini)
        {
            if (!config.MiniMotorEncendido || mini == null || mini.IsDestroyed) return;

            // Sin combustible el juego apaga el motor y las helices se paran
            PonerCombustible(mini);

            try
            {
                if ((mini.flags & BaseEntity.Flags.On) == 0)
                {
                    mini.flags |= BaseEntity.Flags.On;
                    mini.SendNetworkUpdate();
                }
            }
            catch (Exception)
            {
                // si no se puede, el mini vuela igualmente con las helices paradas
            }
        }

        // Vuela como un helicoptero de verdad (sin fisicas, pero con su misma dinamica):
        //  - acelera y frena poco a poco, y reduce la velocidad al girar fuerte
        //  - gira con una velocidad de giro limitada y se inclina (alabeo) hacia el lado del giro
        //  - cabecea hacia delante al acelerar y hacia atras al frenar
        //  - sube y baja con velocidad vertical limitada, siguiendo el relieve
        //  - esquiva edificios, torres y arboles (por encima o por un lado) y nunca los atraviesa
        private void MoverMini(Bot b, float dt, int indice, float ahora)
        {
            BaseVehicle mini = b.Mini;

            if (mini == null || mini.IsDestroyed)
            {
                // Si el mini desaparece, el piloto tambien
                if (b.Npc != null && !b.Npc.IsDestroyed) b.Npc.Kill();
                return;
            }

            // El juego puede volver a activar las fisicas al sentar al piloto: se desactivan siempre
            if (b.Cuerpo != null && !b.Cuerpo.isKinematic) b.Cuerpo.isKinematic = true;

            if (ahora - b.UltimaMotor > 1f)
            {
                b.UltimaMotor = ahora;
                MantenerMotor(mini);
            }

            Vector3 pos = mini.transform.position;
            float vMax = Mathf.Max(3f, config.MiniSpeed);

            // ----- Destino -----
            Vector3 haciaMeta = new Vector3(b.Meta.x - pos.x, 0f, b.Meta.z - pos.z);
            if (!b.TieneMeta || haciaMeta.sqrMagnitude < 1600f || Time.time > b.MetaHasta)
            {
                ElegirMetaMini(b, pos);
                haciaMeta = new Vector3(b.Meta.x - pos.x, 0f, b.Meta.z - pos.z);
            }

            // ----- Obstaculos (5 veces por segundo) -----
            b.SondeoEspera -= dt;
            if (b.SondeoEspera <= 0f)
            {
                b.SondeoEspera = 0.2f;
                SondearVuelo(b, pos, ahora);
            }

            // ----- Rumbo: gira con velocidad limitada, mas despacio cuanto mas rapido va -----
            float rumbo = Mathf.Atan2(haciaMeta.x, haciaMeta.z) * Mathf.Rad2Deg + b.Desvio;
            float error = Mathf.DeltaAngle(b.Giro, rumbo);

            float maxGiro = Mathf.Lerp(55f, 28f, Mathf.Clamp01(b.Vel / vMax)); // grados por segundo
            float giroObj = Mathf.Clamp(error * 1.3f, -maxGiro, maxGiro);
            b.VelGiro = Mathf.MoveTowards(b.VelGiro, giroObj, 70f * dt);
            b.Giro = Mathf.Repeat(b.Giro + b.VelGiro * dt, 360f);

            // ----- Velocidad: acelera poco a poco y frena antes de giros cerrados u obstaculos -----
            float velObj = vMax * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(Mathf.Abs(error) / 100f));
            if (b.Frenar) velObj *= 0.4f;

            float velAntes = b.Vel;
            b.Vel = Mathf.MoveTowards(b.Vel, velObj, (velObj > b.Vel ? 4f : 7f) * dt);
            float aceleracion = (b.Vel - velAntes) / Mathf.Max(0.001f, dt);

            // ----- Altura: sube y baja con suavidad siguiendo el relieve -----
            // La altura objetivo cambia poco a poco (sube rapido si hace falta, baja despacio)
            b.AltSuave = Mathf.MoveTowards(b.AltSuave, b.AltObjetivo, (b.AltObjetivo > b.AltSuave ? 10f : 3f) * dt);
            float vertObj = Mathf.Clamp((b.AltSuave - pos.y) * 0.5f, -4f, 6f);
            if (TerrainMeta.HeightMap != null && pos.y - TerrainMeta.HeightMap.GetHeight(pos) < 12f)
                vertObj = Mathf.Max(vertObj, 6f); // demasiado cerca del suelo: sube ya
            b.VelV = Mathf.MoveTowards(b.VelV, vertObj, 3f * dt);

            // ----- Movimiento: no atraviesa nada -----
            Vector3 fwd = Quaternion.Euler(0f, b.Giro, 0f) * Vector3.forward;
            // Inercia: la velocidad real sigue al morro con retraso, asi en las curvas derrapa un poco
            b.VelH = Vector3.Lerp(b.VelH, fwd * b.Vel, 1f - Mathf.Exp(-dt * 1.8f));
            Vector3 mov = b.VelH * dt;
            mov.y = b.VelV * dt;

            // Balanceo suave de hover (distinto en cada piloto)
            float tb = ahora + b.Fase;
            mov += new Vector3(Mathf.Sin(tb * 0.7f), Mathf.Sin(tb * 1.3f) * 0.6f, Mathf.Cos(tb * 0.5f)) * 0.35f * dt;

            float dist = mov.magnitude;
            RaycastHit golpe;
            if (dist > 0.0001f && Physics.SphereCast(pos, 2.5f, mov / dist, out golpe, dist + 0.5f, maskVuelo,
                QueryTriggerInteraction.Ignore))
            {
                // Choque inminente: se detiene, sube y vuelve a mirar el camino
                b.Vel *= 0.5f;
                b.VelH *= 0.5f;
                b.SondeoEspera = 0f;
                mov = Vector3.up * 3f * dt;
            }

            // ----- Actitud: inclinacion como un helicoptero -----
            float cabeceoObj = Mathf.Clamp(Mathf.Lerp(0f, 14f, b.Vel / vMax) - aceleracion * 1.5f, -10f, 18f)
                               + Mathf.Sin(tb * 0.9f) * 0.8f;
            // Se inclina mas al girar cuanto mas rapido va (como una curva de verdad)
            float alabeoObj = -Mathf.Clamp(b.VelGiro / 55f, -1f, 1f) * 25f * (0.4f + 0.6f * Mathf.Clamp01(b.Vel / vMax))
                              + Mathf.Sin(tb * 1.3f) * 0.8f; // un pequeño balanceo
            float suave = 1f - Mathf.Exp(-dt * 2.5f);
            b.Cabeceo = Mathf.Lerp(b.Cabeceo, cabeceoObj, suave);
            b.Alabeo = Mathf.Lerp(b.Alabeo, alabeoObj, suave);

            b.Mira = fwd;
            Vector3 nueva = pos + mov;

            mini.transform.SetPositionAndRotation(nueva, Quaternion.Euler(b.Cabeceo, b.Giro, b.Alabeo));

            // Cada paso se envia a la red: asi los clientes reciben las posiciones a intervalos regulares
            b.UltimaRed = ahora;
            mini.SendNetworkUpdate_Position();

            if (ahora - b.UltimaCompleta >= 1f)
            {
                b.UltimaCompleta = ahora;
                mini.UpdateNetworkGroup();
            }
        }

        #endregion

        #region Interfaz

        private string Num(float v)
        {
            return v.ToString("0.####", Inv);
        }

        private string Anc(float x, float y)
        {
            return Num(x) + " " + Num(y);
        }

        private string Panel(CuiElementContainer c, string parent, string color, float x0, float y0, float x1, float y1,
            string nombre = null)
        {
            return c.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform = { AnchorMin = Anc(x0, y0), AnchorMax = Anc(x1, y1) }
            }, parent, nombre);
        }

        private void Etiqueta(CuiElementContainer c, string parent, string texto, int tam, string color,
            TextAnchor alineado, float x0, float y0, float x1, float y1, bool negrita = false)
        {
            c.Add(new CuiLabel
            {
                Text =
                {
                    Text = texto,
                    FontSize = tam,
                    Align = alineado,
                    Color = color,
                    Font = negrita ? FuenteNegrita : FuenteNormal
                },
                RectTransform = { AnchorMin = Anc(x0, y0), AnchorMax = Anc(x1, y1) }
            }, parent);
        }

        private void Boton(CuiElementContainer c, string parent, string texto, string comando, bool activo,
            float x0, float y0, float x1, float y1, int tam = 12,
            string fondoOff = ColNaranjaClaro, string textoOff = ColNaranjaOsc)
        {
            c.Add(new CuiButton
            {
                Button = { Command = comando, Color = activo ? ColNaranja : fondoOff },
                RectTransform = { AnchorMin = Anc(x0, y0), AnchorMax = Anc(x1, y1) },
                Text =
                {
                    Text = texto,
                    FontSize = tam,
                    Align = TextAnchor.MiddleCenter,
                    Color = activo ? ColBlanco : textoOff,
                    Font = FuenteNegrita
                }
            }, parent);
        }

        private void Interruptor(CuiElementContainer c, string parent, string texto, string comando, bool activo,
            float x0, float y0, float x1, float y1, int tam = 12)
        {
            Boton(c, parent, texto + (activo ? ": SÍ" : ": NO"), comando, activo,
                x0, y0, x1, y1, tam, ColApagado, ColTexto);
        }

        private void Entrada(CuiElementContainer c, string parent, string texto, string comando, int limite, int tam,
            float x0, float y0, float x1, float y1)
        {
            string caja = Panel(c, parent, ColApagado, x0, y0, x1, y1);
            c.Add(new CuiElement
            {
                Parent = caja,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = texto,
                        FontSize = tam,
                        Align = TextAnchor.MiddleCenter,
                        Color = ColTexto,
                        CharsLimit = limite,
                        Command = comando,
                        Font = FuenteNegrita
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
        }

        // Fila de ajuste:  Nombre   [-grande] [-pequeño] [ valor ] [+pequeño] [+grande]   unidad
        private void FilaAjuste(CuiElementContainer c, string parent, string campo, string nombre, string unidad,
            string valor, float grande, float pequeno, float y1)
        {
            float y0 = y1 - 0.08f;

            Etiqueta(c, parent, nombre, 14, ColTexto, TextAnchor.MiddleLeft, 0.04f, y0, 0.27f, y1, true);

            Boton(c, parent, "−" + Num(grande), "arena.ui paso " + campo + " " + Num(-grande), false,
                0.28f, y0, 0.375f, y1, 13);
            Boton(c, parent, "−" + Num(pequeno), "arena.ui paso " + campo + " " + Num(-pequeno), false,
                0.385f, y0, 0.48f, y1, 13);

            Entrada(c, parent, valor, "arena.ui set " + campo, 6, 16, 0.49f, y0, 0.69f, y1);

            Boton(c, parent, "+" + Num(pequeno), "arena.ui paso " + campo + " " + Num(pequeno), false,
                0.70f, y0, 0.795f, y1, 13);
            Boton(c, parent, "+" + Num(grande), "arena.ui paso " + campo + " " + Num(grande), false,
                0.805f, y0, 0.90f, y1, 13);

            Etiqueta(c, parent, unidad, 12, ColSuave, TextAnchor.MiddleLeft, 0.91f, y0, 0.99f, y1);
        }

        private string TextoVivos()
        {
            return "NPC vivos: " + NpcVivos() + " / " + TotalNpc();
        }

        private CuiElement CrearVivos(string texto, bool reemplazar)
        {
            return new CuiElement
            {
                Name = UiVivos,
                Parent = UiCardEstado,
                DestroyUi = reemplazar ? UiVivos : null,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = texto,
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = ColTexto,
                        Font = FuenteNegrita
                    },
                    new CuiRectTransformComponent { AnchorMin = Anc(0.05f, 0.60f), AnchorMax = Anc(0.95f, 0.70f) }
                }
            };
        }

        private string Cortar(string texto, int max)
        {
            return texto.Length <= max ? texto : texto.Substring(0, max - 1) + "…";
        }

        // Refresco de 1 s mientras haya algun panel abierto
        private void AjustarTick()
        {
            if (abiertos.Count > 0)
            {
                if (tickUi == null || tickUi.Destroyed)
                    tickUi = timer.Every(1f, TickUi);
            }
            else if (tickUi != null)
            {
                tickUi.Destroy();
                tickUi = null;
            }
        }

        private void TickUi()
        {
            if (abiertos.Count == 0)
            {
                AjustarTick();
                return;
            }

            string texto = TextoVivos();

            foreach (ulong id in new List<ulong>(abiertos))
            {
                BasePlayer jugador = BasePlayer.FindByID(id);
                if (jugador == null || !jugador.IsConnected)
                {
                    abiertos.Remove(id);
                    ultimoVivos.Remove(id);
                    continue;
                }

                string ultimo;
                if (ultimoVivos.TryGetValue(id, out ultimo) && ultimo == texto) continue;

                ultimoVivos[id] = texto;
                var c = new CuiElementContainer();
                c.Add(CrearVivos(texto, true));
                CuiHelper.AddUi(jugador, c);
            }

            AjustarTick();
        }

        private void MostrarUi(BasePlayer jugador, bool abrir = false)
        {
            // Al refrescar solo se recrea el contenido, en el mismo mensaje que borra el anterior:
            // asi no parpadea y el cursor no se recoloca en cada pulsacion.
            bool yaAbierta = !abrir && abiertos.Contains(jugador.userID);
            if (!yaAbierta) CuiHelper.DestroyUi(jugador, UiFondo);

            var c = new CuiElementContainer();

            if (!yaAbierta)
            {
                c.Add(new CuiPanel
                {
                    Image = { Color = ColFondo },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    CursorEnabled = true
                }, "Overlay", UiFondo);
                abiertos.Add(jugador.userID);
            }
            AjustarTick();

            c.Add(new CuiElement
            {
                Name = UiContenido,
                Parent = UiFondo,
                DestroyUi = yaAbierta ? UiContenido : null,
                Components =
                {
                    new CuiImageComponent { Color = ColPanel },
                    new CuiRectTransformComponent { AnchorMin = "0.12 0.08", AnchorMax = "0.88 0.92" }
                }
            });
            string main = UiContenido;

            // ---------- Cabecera ----------
            string cab = Panel(c, main, ColNaranja, 0f, 0.9f, 1f, 1f);
            Etiqueta(c, cab, "Arena", 30, ColBlanco, TextAnchor.LowerLeft, 0.025f, 0.38f, 0.5f, 1f, true);
            Etiqueta(c, cab, "NPC con aspecto de jugador para practicar puntería  ·  Hecho por SCR98", 12, ColBlancoSuave,
                TextAnchor.UpperLeft, 0.027f, 0.0f, 0.7f, 0.4f);

            Boton(c, cab, "ACTUALIZAR", "arena.ui refrescar", false, 0.74f, 0.27f, 0.86f, 0.73f, 12, ColBlanco, ColNaranjaOsc);
            Boton(c, cab, "CERRAR", "arena.ui cerrar", false, 0.875f, 0.27f, 0.975f, 0.73f, 12, ColBlanco, ColNaranjaOsc);

            // ---------- Tarjeta ESTADO ----------
            string estado = Panel(c, main, ColTarjeta, 0.02f, 0.38f, 0.40f, 0.87f, UiCardEstado);
            Etiqueta(c, estado, "ESTADO", 13, ColNaranja, TextAnchor.MiddleLeft, 0.05f, 0.90f, 0.95f, 0.98f, true);
            Etiqueta(c, estado, arenaActiva ? "ACTIVA" : "DETENIDA", 34, arenaActiva ? ColNaranjaOsc : ColSuave,
                TextAnchor.MiddleCenter, 0.05f, 0.72f, 0.95f, 0.90f, true);

            string textoVivos = TextoVivos();
            ultimoVivos[jugador.userID] = textoVivos;
            c.Add(CrearVivos(textoVivos, false));

            Etiqueta(c, estado, "Los NPC se reparten en anillos a tu alrededor", 11, ColSuave,
                TextAnchor.MiddleCenter, 0.05f, 0.50f, 0.95f, 0.58f);

            Boton(c, estado, "INICIAR ARENA", "arena.ui iniciar", !arenaActiva, 0.05f, 0.29f, 0.95f, 0.47f, 15);
            Boton(c, estado, "DETENER ARENA", "arena.ui detener", arenaActiva, 0.05f, 0.08f, 0.95f, 0.26f, 15);

            // ---------- Tarjeta AJUSTES ----------
            string ajustes = Panel(c, main, ColTarjeta, 0.41f, 0.38f, 0.98f, 0.87f);
            Etiqueta(c, ajustes, "AJUSTES", 13, ColNaranja, TextAnchor.MiddleLeft, 0.04f, 0.90f, 0.3f, 0.98f, true);
            Etiqueta(c, ajustes, "Escribe un valor y pulsa Enter", 11, ColSuave, TextAnchor.MiddleRight,
                0.3f, 0.90f, 0.96f, 0.98f);

            FilaAjuste(c, ajustes, "quietos", "Quietos", "NPC", config.CantQuietos.ToString(Inv), 10f, 1f, 0.875f);
            FilaAjuste(c, ajustes, "correr", "Corriendo", "NPC", config.CantCorriendo.ToString(Inv), 10f, 1f, 0.790f);
            FilaAjuste(c, ajustes, "caminar", "Caminando", "NPC", config.CantCaminando.ToString(Inv), 10f, 1f, 0.705f);
            FilaAjuste(c, ajustes, "mini", "Mini 1 persona", "minis", config.CantMinis.ToString(Inv), 5f, 1f, 0.620f);
            FilaAjuste(c, ajustes, "minidoble", "Mini 2 personas", "minis", config.CantMinisDobles.ToString(Inv), 5f, 1f, 0.535f);
            FilaAjuste(c, ajustes, "radio", "Radio de la arena", "m", Num(config.Radius), 50f, 10f, 0.450f);
            FilaAjuste(c, ajustes, "distancia", "Distancia mínima", "m", Num(config.MinSpawnDistance), 25f, 5f, 0.365f);
            FilaAjuste(c, ajustes, "anillos", "Anillos para repartir", "anillos", config.Rings.ToString(Inv), 5f, 1f, 0.280f);
            FilaAjuste(c, ajustes, "reaparicion", "Reaparición", "s", Num(config.RespawnSeconds), 5f, 1f, 0.195f);

            Interruptor(c, ajustes, "Munición infinita", "arena.ui municion", config.InfinitePlayerAmmo,
                0.03f, 0.01f, 0.21f, 0.09f, 10);
            Interruptor(c, ajustes, "Sin desgaste", "arena.ui desgaste", config.NoWeaponWear,
                0.22f, 0.01f, 0.40f, 0.09f, 10);
            Interruptor(c, ajustes, "Yo inmortal", "arena.ui inmortal", config.PlayerImmortal,
                0.41f, 0.01f, 0.59f, 0.09f, 10);
            Interruptor(c, ajustes, "Bots inmortales", "arena.ui botsinmortales", config.BotsImmortal,
                0.60f, 0.01f, 0.78f, 0.09f, 10);
            Interruptor(c, ajustes, "Aparecen a la vista", "arena.ui visible", config.SpawnVisible,
                0.79f, 0.01f, 0.97f, 0.09f, 10);

            // ---------- Tarjeta CONJUNTOS DE ROPA ----------
            string ropa = Panel(c, main, ColTarjeta, 0.02f, 0.03f, 0.72f, 0.36f);
            Etiqueta(c, ropa, "CONJUNTOS DE ROPA", 13, ColNaranja, TextAnchor.MiddleLeft, 0.02f, 0.88f, 0.4f, 0.98f, true);
            Etiqueta(c, ropa, "Cada NPC se viste con uno al azar", 11, ColSuave, TextAnchor.MiddleRight,
                0.4f, 0.88f, 0.98f, 0.98f);

            Etiqueta(c, ropa, "Ponte la ropa que quieras y guárdala:", 12, ColTexto, TextAnchor.MiddleLeft,
                0.02f, 0.68f, 0.40f, 0.84f, true);
            Etiqueta(c, ropa, "Nombre (opcional) y Enter para guardar", 11, ColSuave, TextAnchor.MiddleLeft,
                0.02f, 0.56f, 0.40f, 0.68f);
            Entrada(c, ropa, "", "arena.ui guardar", 24, 14, 0.02f, 0.38f, 0.40f, 0.55f);
            Boton(c, ropa, "GUARDAR ROPA ACTUAL", "arena.ui guardar", false, 0.02f, 0.14f, 0.40f, 0.34f, 13);

            var nombres = NombresConjuntos();
            if (nombres.Count == 0)
            {
                Etiqueta(c, ropa, "Sin conjuntos guardados: los NPC llevarán ropa básica", 12, ColSuave,
                    TextAnchor.MiddleCenter, 0.43f, 0.10f, 0.98f, 0.84f);
            }
            else
            {
                int mostrar = Mathf.Min(nombres.Count, 8);
                for (int i = 0; i < mostrar; i++)
                {
                    int fila = i / 2;
                    int col = i % 2;
                    float x0 = 0.43f + col * 0.28f;
                    float y1 = 0.84f - fila * 0.19f;
                    float y0 = y1 - 0.16f;

                    string chip = Panel(c, ropa, ColApagado, x0, y0, x0 + 0.265f, y1);
                    Etiqueta(c, chip, Cortar(nombres[i], 16), 12, ColTexto, TextAnchor.MiddleLeft,
                        0.05f, 0f, 0.78f, 1f, true);
                    Boton(c, chip, "X", "arena.ui borrar " + i, false, 0.80f, 0.12f, 0.96f, 0.88f, 12);
                }

                if (nombres.Count > mostrar)
                    Etiqueta(c, ropa, "+" + (nombres.Count - mostrar) + " más", 11, ColSuave, TextAnchor.MiddleRight,
                        0.80f, 0.02f, 0.98f, 0.10f);
            }

            // ---------- Tarjeta MARCADOR DE DISTANCIA ----------
            string marc = Panel(c, main, ColTarjeta, 0.73f, 0.03f, 0.98f, 0.36f);
            Etiqueta(c, marc, "MARCADOR DE DISTANCIA", 13, ColNaranja, TextAnchor.MiddleCenter, 0.03f, 0.88f, 0.97f, 0.98f, true);
            Interruptor(c, marc, "Mostrar distancia", "arena.ui marcador", config.ShowHitDistance,
                0.06f, 0.70f, 0.94f, 0.85f, 12);

            // Pantalla en miniatura: se pulsa la esquina donde quieres el cuadro
            string pantalla = Panel(c, marc, ColApagado, 0.10f, 0.06f, 0.90f, 0.64f);
            Etiqueta(c, pantalla, "Elige la esquina", 10, ColSuave, TextAnchor.MiddleCenter, 0.30f, 0.35f, 0.70f, 0.65f);
            Boton(c, pantalla, config.HitCorner == 0 ? "AQUÍ" : "", "arena.ui esquina 0", config.HitCorner == 0,
                0.03f, 0.60f, 0.30f, 0.94f, 10);
            Boton(c, pantalla, config.HitCorner == 1 ? "AQUÍ" : "", "arena.ui esquina 1", config.HitCorner == 1,
                0.70f, 0.60f, 0.97f, 0.94f, 10);
            Boton(c, pantalla, config.HitCorner == 2 ? "AQUÍ" : "", "arena.ui esquina 2", config.HitCorner == 2,
                0.70f, 0.06f, 0.97f, 0.40f, 10);
            Boton(c, pantalla, config.HitCorner == 3 ? "AQUÍ" : "", "arena.ui esquina 3", config.HitCorner == 3,
                0.03f, 0.06f, 0.30f, 0.40f, 10);

            CuiHelper.AddUi(jugador, c);
        }

        #endregion
        #region Parche IsBot

        // Igual que HumanNPC: el juego solo registra bien en la red a los jugadores sin conexion
        // si los considera bots. Los bots de la arena usan ids de este rango.
        private const ulong IdBotBase = 76561198500000000UL;
        private const ulong IdBotTope = 76561198600000000UL;

        [Oxide.Core.Plugins.AutoPatch]
        [HarmonyLib.HarmonyPatch(typeof(BasePlayer), "get_IsBot")]
        internal class ParcheIsBot
        {
            [HarmonyLib.HarmonyPrefix]
            internal static bool Prefix(BasePlayer __instance, ref bool __result)
            {
                if ((object)__instance == null) return true;

                ulong id = __instance.userID;
                if (id < IdBotBase || id >= IdBotTope) return true;

                __result = true;
                return false;
            }
        }

        #endregion
    }
}
