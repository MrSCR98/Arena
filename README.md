# Arena - Campo de tiro con NPC para Rust (Oxide)

Un plugin para **Rust** que crea una **arena de NPC con aspecto de jugador** para practicar puntería. Los NPC corren, caminan, esquivan obstáculos, saltan y hasta pilotan **minihelicópteros**. Todo se controla desde un panel visual, **sin tener que escribir comandos de consola**.

Escribes `/arena`, pulsas **INICIAR ARENA** y los NPC aparecen a tu alrededor.

---

## 🚀 ¿Qué hace este plugin?

- Crea una **arena circular** centrada en el sitio donde estás cuando la inicias
- Genera **5 tipos de NPC**: quietos, corriendo, caminando, minihelicóptero de 1 persona y minihelicóptero de 2 personas
- Los NPC se visten como jugadores normales, con los **conjuntos de ropa (y arma) que tú guardes**
- Reparte a los NPC en **anillos** para que haya objetivos cerca y lejos
- Cuando matas a un NPC, **reaparece** al cabo de unos segundos
- Te da **munición infinita**, **armas sin desgaste** y te hace **inmortal** (todo se puede desactivar)
- Muestra la **distancia** a la que has acertado a cada NPC en un cuadro en pantalla
- Los NPC no sueltan **ni cuerpo ni objetos** al morir, así el suelo siempre queda limpio
- Los animales, científicos, Bradley y helicóptero del mapa **no pueden hacer daño a los NPC** de la arena
- **Guarda tus ajustes**: al reiniciar el servidor se mantienen
- Solo lo pueden usar **admins** o quien tenga el permiso

⚠️ El panel es solo para ti, pero la arena es **del servidor**: los NPC existen para todos los jugadores y solo hay una arena activa a la vez.

⚠️ **Rendimiento:** cuantos más NPC pongas, **más bajan los FPS**. Cada NPC es un jugador completo (con modelo, ropa y arma) y los minihelicópteros pesan todavía más. Con cifras altas notarás la bajada en tu juego y en el de los demás jugadores. Empieza con pocos y ve subiendo hasta encontrar un número con el que vayas fluido.

---

## 📦 Requisitos

- Un servidor de Rust con **Oxide / uMod**
- El plugin incluye un pequeño parche de Harmony (`IsBot`) que Oxide aplica solo. No tienes que hacer nada

---

## 📥 Instalación

1. Copia `Arena.cs` en la carpeta de plugins del servidor:

```text
oxide/plugins/Arena.cs
```

2. El servidor lo carga solo. Si quieres hacerlo a mano, desde la consola del servidor:

```text
oxide.reload Arena
```

Archivos que crea el plugin:

| Archivo | Qué guarda |
|---------|-----------|
| `oxide/config/Arena.json` | Todos los ajustes del panel |
| `oxide/data/Arena_Outfits.json` | Los conjuntos de ropa guardados |

---

## 🎮 Cómo se usa

Dentro del juego, escribe en el chat:

```text
/arena
```

Se abre el panel. Para cerrarlo pulsa **CERRAR**.

El panel tiene cuatro zonas: **Estado** (arriba izquierda), **Ajustes** (arriba derecha), **Conjuntos de ropa** (abajo izquierda) y **Marcador de distancia** (abajo derecha).

💡 Los valores del panel se **guardan al momento** y los cambios se aplican **con la arena en marcha**: si subes o bajas la cantidad de NPC, el plugin añade o quita NPC sin reiniciar nada.

---

## 🖱️ Los botones, uno por uno

### 🔝 Cabecera

| Botón | Qué hace |
|-------|----------|
| **ACTUALIZAR** | Vuelve a dibujar el panel con los valores actuales |
| **CERRAR** | Cierra el panel. La arena **sigue funcionando** aunque lo cierres |

---

### 📊 Tarjeta ESTADO

Muestra si la arena está **ACTIVA** o **DETENIDA** y cuántos NPC hay vivos (`NPC vivos: 12 / 54`). El contador se actualiza cada segundo mientras el panel está abierto. Un minihelicóptero de 2 personas cuenta como 2 NPC.

| Botón | Qué hace |
|-------|----------|
| **INICIAR ARENA** | Arranca la arena. **El centro es el punto exacto donde estás en ese momento**, y los NPC quietos te miran a ti. Se pone en naranja cuando la arena está parada |
| **DETENER ARENA** | Borra todos los NPC y minihelicópteros, quita el cuadro de distancia y devuelve tu personaje a la normalidad. Se pone en naranja cuando la arena está activa |

💡 Para cambiar el centro de la arena: pulsa **DETENER ARENA**, ve a otro sitio y pulsa **INICIAR ARENA**.

---

### ⚙️ Tarjeta AJUSTES

Cada fila de ajuste tiene el mismo formato:

```text
Nombre   [−grande] [−pequeño]    valor    [+pequeño] [+grande]   unidad
```

- Los **botones** suben o bajan el valor
- **Caja del valor:** escribe un número exacto y pulsa **Enter**

| Ajuste | Botones | Límites | Qué hace |
|--------|---------|---------|----------|
| **Quietos** | ±10 / ±1 | 0 a 500 | NPC que se quedan parados y te miran. Son los más fáciles para practicar puntería a distancia |
| **Corriendo** | ±10 / ±1 | 0 a 500 | NPC que corren por la arena esquivando obstáculos y saltando lo que pueden |
| **Caminando** | ±10 / ±1 | 0 a 500 | NPC que caminan por la arena. Más lentos que los que corren |
| **Mini 1 persona** | ±5 / ±1 | desde 0 | Minihelicópteros con un solo piloto que vuelan por la arena |
| **Mini 2 personas** | ±5 / ±1 | desde 0 | Minihelicópteros con piloto y pasajero. Cada uno suma 2 NPC al total |
| **Radio de la arena** | ±50 / ±10 | 10 a 2000 m | Tamaño de la arena, medido desde el centro |
| **Distancia mínima** | ±25 / ±5 | 0 a 500 m | Distancia mínima a los jugadores a la que puede **aparecer** un NPC. Así no te aparecen encima |
| **Anillos para repartir** | ±5 / ±1 | 1 a 20 | En cuántos anillos se divide el radio. Cada anillo recibe los mismos NPC, así hay objetivos cerca y lejos y no solo en el borde |
| **Reaparición** | ±5 / ±1 | 1 a 600 s | Segundos que tarda en reaparecer un NPC muerto |

⚠️ **Ojo con las cantidades:** los ajustes de **Quietos**, **Corriendo**, **Caminando** y sobre todo los **minihelicópteros** son los que más FPS te quitan. Con valores altos el juego puede ir bastante peor. Si notas tirones, baja primero los minis y los NPC que corren.

💡 Con el radio en 200 m y 10 anillos, cada anillo mide 20 m. Con 1 solo anillo los NPC se reparten sin ningún orden por toda la arena.

#### 🔘 Interruptores

Cada interruptor muestra **SÍ** (naranja) o **NO** (gris).

| Interruptor | Por defecto | Qué hace |
|-------------|-------------|----------|
| **Munición infinita** | SÍ | Tu cargador se rellena solo al disparar. No tienes que recargar |
| **Sin desgaste** | SÍ | Tus armas y herramientas no pierden condición |
| **Yo inmortal** | SÍ | No recibes daño y la vida, la comida y el agua se mantienen siempre al máximo |
| **Bots inmortales** | NO | Los NPC no reciben daño y no se pueden matar. Útil para probar tu puntería sin que desaparezcan |
| **Aparecen a la vista** | NO | Los NPC solo aparecen en sitios donde tú los puedas ver (en cualquier dirección, vale con girarte). Si en varios intentos no hay sitio visible, se relaja la condición para que no se queden sin aparecer. Los minihelicópteros no cuentan |

⚠️ **Munición infinita**, **Sin desgaste** e **Yo inmortal** solo funcionan **mientras la arena está activa** y solo para quien tenga permiso. Al pulsar **DETENER ARENA** dejan de aplicarse.

---

### 👕 Tarjeta CONJUNTOS DE ROPA

Cada NPC se viste con **un conjunto elegido al azar** de los que guardes. Si no hay ninguno guardado, los NPC llevan ropa básica (sudadera, pantalones y botas).

| Elemento | Qué hace |
|----------|----------|
| **Caja de nombre** | Escribe un nombre (hasta 24 letras) y pulsa **Enter** para guardar tu ropa con ese nombre. Si ya existe un conjunto con ese nombre, lo sustituye |
| **GUARDAR ROPA ACTUAL** | Guarda lo que llevas puesto. Si lo pulsas sin escribir nombre, se llama `Conjunto 1`, `Conjunto 2`... |
| **X** (en cada conjunto) | Borra ese conjunto |

**Qué se guarda:**

- Toda la **ropa que llevas equipada**, con sus skins
- El **arma o herramienta que tengas en la mano**, si es un arma de fuego o cuerpo a cuerpo. Los NPC la llevarán en la mano
- Si no llevas ropa ni arma, no guarda nada y el plugin te avisa

💡 Los NPC que pilotan un minihelicóptero llevan la ropa del conjunto pero **sin arma**, porque van sentados.

💡 El panel enseña hasta **8 conjuntos**. Si hay más, aparece el texto `+N más`, pero siguen funcionando.

---

### 🎯 Tarjeta MARCADOR DE DISTANCIA

Cada vez que aciertas a un NPC, aparece un cuadro en pantalla con la distancia a la que estaba (por ejemplo `87.3 m`). Con la arena activa, el cuadro se queda fijo mostrando **tu último acierto** (en blanco hasta el primero).

| Botón | Qué hace |
|-------|----------|
| **Mostrar distancia** | Activa o desactiva el cuadro de distancia |
| **Esquinas de la pantalla en miniatura** | Pulsa la esquina donde quieres el cuadro: arriba izquierda, arriba derecha, abajo derecha o abajo izquierda. La esquina elegida muestra **AQUÍ** |

💡 Al cambiar la esquina con la arena parada, el cuadro aparece unos segundos con el texto `123.4 m` para que veas cómo queda.

---

## 🤖 Cómo se comportan los NPC

| Tipo | Comportamiento |
|------|---------------|
| **Quietos** | No se mueven. Te miran a ti, que eres quien inició la arena |
| **Corriendo** | Recorren la arena corriendo. Esquivan obstáculos, saltan los bajos y rodean los que son más altos de 1,5 m |
| **Caminando** | Igual que los anteriores, pero más despacio |
| **Mini 1 persona** | Un piloto en un minihelicóptero. Vuela por la arena esquivando obstáculos. Si matas al piloto, el mini **desaparece sin explotar** y sin dejar nada |
| **Mini 2 personas** | Piloto y pasajero. Si matas al **piloto**, el mini se queda sin motor y **cae con físicas reales** con el pasajero dentro. Si el mini queda destruido en la caída, el pasajero muere. Al aterrizar entero, el mini y el pasajero desaparecen sin restos |

Cuando un NPC muere:

- No deja **cuerpo ni objetos** (ni el arma, ni la ropa)
- **Reaparece en el mismo hueco** (mismo anillo y mismo sector) pasados los segundos que pongas en **Reaparición**, así el reparto de la arena se mantiene
- El pasajero de un mini no reaparece solo: se vuelve a crear junto con su piloto cuando este reaparece

Otros detalles:

- Los NPC **nunca aparecen** dentro de un árbol, una roca, una pared o el agua
- Los **animales** que haya dentro de la arena (y 60 m alrededor) se eliminan mientras la arena está activa, porque no hay forma fiable de impedir que persigan a los NPC
- Si hay muchos NPC lejos de los jugadores, el plugin los hace "pensar" menos veces para no cargar el servidor

---

## 🔑 Permisos (para gente que no es admin)

Por defecto, solo pueden usar `/arena` los **admins del servidor**.

Si quieres que lo use otra gente (moderadores, ayudantes...), dales el permiso:

```text
arena.admin
```

Ejecuta estos comandos en la **consola del servidor o por RCON**.

### 👤 Dar el permiso a un jugador

```text
oxide.grant user NombreDelJugador arena.admin
oxide.grant user 76561198000000000 arena.admin
```

Puedes usar el nombre o el SteamID.

### 👥 Dar el permiso a un grupo

```text
oxide.group add moderadores
oxide.grant group moderadores arena.admin
oxide.usergroup add NombreDelJugador moderadores
```

Así, todo el que esté en el grupo `moderadores` puede usar el panel.

### 🚫 Quitar el permiso

```text
oxide.revoke user NombreDelJugador arena.admin
oxide.revoke group moderadores arena.admin
```

### 🔎 Ver quién tiene el permiso

```text
oxide.show perm arena.admin
```

⚠️ Quien tenga este permiso puede crear y quitar NPC en el servidor y cambiar los ajustes de la arena. Dalo solo a gente de confianza.

---

## ⌨️ Comandos

### 💬 Chat

| Comando | Qué hace |
|---------|----------|
| `/arena` | Abre el panel de la arena |

Si no tienes permiso, el plugin te lo dice.

### 🖥️ Consola (para usuarios avanzados)

Los botones del panel usan internamente el comando `arena.ui`. También puedes lanzarlo a mano **desde la consola de F1 dentro del juego**, siempre que tengas permiso. No funciona desde la consola del servidor ni por RCON, porque necesita un jugador.

| Comando | Qué hace |
|---------|----------|
| `arena.ui iniciar` | Inicia la arena en tu posición actual |
| `arena.ui detener` | Detiene la arena y borra todos los NPC |
| `arena.ui set <ajuste> <valor>` | Fija un valor |
| `arena.ui paso <ajuste> <cantidad>` | Suma o resta una cantidad al valor |
| `arena.ui municion` | Activa o desactiva «Munición infinita» |
| `arena.ui desgaste` | Activa o desactiva «Sin desgaste» |
| `arena.ui inmortal` | Activa o desactiva «Yo inmortal» |
| `arena.ui botsinmortales` | Activa o desactiva «Bots inmortales» |
| `arena.ui visible` | Activa o desactiva «Aparecen a la vista» |
| `arena.ui marcador` | Activa o desactiva el cuadro de distancia |
| `arena.ui esquina <0-3>` | Cambia la esquina del cuadro: `0` arriba izq., `1` arriba der., `2` abajo der., `3` abajo izq. |
| `arena.ui guardar <nombre>` | Guarda tu ropa actual (el nombre es opcional) |
| `arena.ui borrar <nº>` | Borra un conjunto (el número empieza en `0`, por orden alfabético) |
| `arena.ui refrescar` | Vuelve a dibujar el panel |
| `arena.ui cerrar` | Cierra el panel |

Los nombres de ajuste son: `quietos`, `correr`, `caminar`, `mini`, `minidoble`, `radio`, `distancia`, `anillos` y `reaparicion`.

Ejemplos:

```text
arena.ui set radio 300
arena.ui paso quietos 10
arena.ui set reaparicion 3.5
```

---

## 🛠️ Ajustes extra (solo en el archivo de configuración)

Hay unos pocos ajustes que **no tienen botón** en el panel. Se cambian editando `oxide/config/Arena.json` y recargando el plugin con `oxide.reload Arena`.

| Ajuste | Por defecto | Qué hace |
|--------|-------------|----------|
| `RunSpeed` | `5.5` | Velocidad de los NPC que corren |
| `WalkSpeed` | `2.8` | Velocidad de los NPC que caminan |
| `MiniSpeed` | `18` | Velocidad de los minihelicópteros |
| `RoamFullArena` | `true` | `true`: los NPC recorren toda la arena. `false`: solo patrullan cerca de donde aparecieron |
| `MiniMotorEncendido` | `true` | Las hélices de los minihelicópteros giran |
| `RemoveAnimals` | `true` | Elimina los animales que haya dentro de la arena mientras está activa |

---

## 🛡️ Cómo funciona por dentro

- La arena es **una sola**: si ya está activa, pulsar **INICIAR ARENA** de nuevo no hace nada
- Los NPC son jugadores de verdad con un ID propio, así se ven con modelo de jugador completo
- Se crean **pocos por tick** (máximo 3 cada 0,1 s) para no bloquear el servidor
- Los huecos libres se rellenan solos: si subes la cantidad, aparecen NPC nuevos; si la bajas, se quitan los de los huecos más altos
- Si un NPC da un error al moverse, se retira y se crea otro
- El panel solo se actualiza cada segundo **mientras está abierto**
- Al descargar el plugin o reiniciar el servidor, la arena se detiene y se **eliminan todos los NPC y minihelicópteros**, sin dejar restos
- Si tenías datos del plugin antiguo `RandomNPCArena`, los conjuntos de ropa se importan solos

---

## 👤 Autor

Hecho por **SCR98**.
