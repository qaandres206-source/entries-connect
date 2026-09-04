# ControlPanelPSA — Tareas pendientes de UI/UX y rendimiento

> Documento de traspaso. Está escrito para que **otro asistente pueda trabajar sin más
> contexto que este archivo y el repositorio**. Cada tarea dice qué pasa, dónde, por qué
> importa y cómo comprobar que quedó bien.
>
> Origen: auditoría de 2026-09-03 en cinco perspectivas (rendimiento, móvil, escritorio,
> accesibilidad, estados de error), con cada hallazgo verificado contra el código antes de
> anotarlo. Los que no se sostuvieron están al final, en «Descartado», para que nadie los
> vuelva a proponer.

---

## 1. El proyecto en dos minutos

App **Blazor Server (.NET 9)** que usan **2-3 consultores de Intwo** para registrar
entradas de tiempo y notas en **ConnectWise Manage**, ver sus tickets asignados y consultar
contexto de **ITGlue**.

| | |
|---|---|
| UI | `cwApp/Components/Pages/Home.razor` (~1380 líneas: board, detalle, hilo, formulario, modal de configuración, visor de imágenes) |
| Estilos | `cwApp/wwwroot/app.css` (~1200 líneas) |
| Servicios | `cwApp/Services/` — `ConnectWiseService`, `ItGlueService`, `TicketContextService`, `ReportService`, `NoteImageProxy`, `NoteContentParser`, `ConfigStorageService` |
| Arranque | `cwApp/Program.cs` |
| Head / PWA | `cwApp/Components/App.razor` |
| JS | `cwApp/wwwroot/download.js` |

**Credenciales:** cada consultor mete su Member ID + public/private key de ConnectWise.
Se guardan en `localStorage` del navegador. **No hay base de datos ni login.**

**Despliegue: Render, plan Free, vía Docker.** Esto condiciona casi todo:

- 512 MB de RAM, **una sola instancia**, CPU compartida.
- **Se duerme tras ~15 min sin tráfico.** Al volver, el circuito SignalR del servidor ya no
  existe: reconectar es imposible y hay que recargar.
- Cada interacción del usuario es un ida y vuelta por WebSocket.
- Se usa **en escritorio y en iPhone como PWA instalada** en la pantalla de inicio. Debe
  funcionar bien en ambos.

### Ya está hecho — no lo vuelvas a proponer

Fusión de notas del ticket con notas de entradas de tiempo (con deduplicación) · conversión
de horas UTC a la zona del usuario · hilo con la nota más reciente arriba · render de
imágenes de las notas con proxy autenticado y visor a pantalla completa · filtro por company,
palabra clave y «no asignados» · exportación a `.xlsx` · dashboard a 2 columnas · PWA instalable.

---

## 2. Reglas que no se negocian

**Commits.** Sin excepción:

```bash
git -c user.name="Andres F. Mora" -c user.email="andres.mora@intwo.cloud" commit -m "..."
```

**Nunca** añadas un trailer `Co-Authored-By: Claude`. Mensajes en español, explicando *por
qué*, no solo *qué*.

**Push a los dos remotos.** Azure DevOps es el repo de referencia; GitHub es el espejo que
Render vigila para desplegar. Si solo empujas a `origin`, **no se despliega nada**:

```bash
git push origin main && git push github main
```

**Seguridad — invariantes que no se tocan:**

1. `NoteImageProxy.IsOwnConnectWiseUrl` **solo** admite URLs `https` del mismo host que
   `config.SiteUrl`. Sin eso, una nota manipulada (`![x](https://atacante/)`) haría que el
   servidor enviara la **Basic auth del usuario** a un servidor ajeno. El cuerpo de una nota
   es contenido no confiable: un cliente puede escribir al ticket por correo.
2. `NoteContentParser` **nunca genera HTML ni `MarkupString`**. Devuelve datos que Razor
   escapa. No lo «optimices» a HTML crudo.
3. El puente de ITGlue es de **solo lectura** y **jamás** consulta `/passwords`.
4. La API key de ITGlue vive **solo** en variables de entorno de Render. Nunca en el repo.

---

## 3. Cómo verificar tu trabajo

El SDK de .NET 9 está en `~/.dotnet` (no en el PATH por defecto).

```bash
export PATH="$HOME/.dotnet:$PATH" && dotnet build cwApp/cwApp.csproj -v q --nologo
```

Debe salir **0 advertencias, 0 errores**.

Prueba de humo (la app no debe escupir excepciones al arrancar):

```bash
export PATH="$HOME/.dotnet:$PATH" && dotnet run --project cwApp/cwApp.csproj --no-launch-profile --urls "http://localhost:5403"
```

**No hay proyecto de tests.** La lógica pura se prueba creando un proyecto de consola
desechable fuera del repo que referencie `cwApp/cwApp.csproj` y ejercite los métodos
`public static` (`ConnectWiseService.MergeThread`, `ToUserLocal`, `ToApiUtc`,
`NoteContentParser.Parse`, `NoteImageProxy.IsOwnConnectWiseUrl`). Si añades lógica pura,
hazla `public static` para poder probarla sin red.

**Verificación visual sin credenciales de ConnectWise.** Truco ya probado: escribe un HTML
de andamiaje que enlace el `app.css` real con el markup que quieras revisar, y captúralo con
Chrome headless:

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --no-sandbox --allow-file-access-from-files --hide-scrollbars --virtual-time-budget=4000 --screenshot=out.png --window-size=1280,800 "file:///ruta/andamio.html"
```

> **Trampa importante:** `--window-size` **no** equivale al viewport CSS (pedir 390 dio 485).
> Para medir desbordamiento horizontal de verdad, embebe la página en un
> `<iframe width="390">` y compara `scrollWidth` contra `clientWidth` **dentro** del iframe.
> Medir sobre la ventana headless da falsos positivos.

> **Los números de línea de este documento son de 2026-09-03.** Se desplazan en cuanto
> edites. Localiza por el fragmento de código citado, no por el número.

---

## 4. Tareas

Ordenadas por valor entregado frente a esfuerzo. **Un commit por tarea**, para poder
revertir una sin arrastrar las demás.

---

### T1 · Quitar `oninput` de los campos que no filtran en vivo
**Esfuerzo: bajo · Impacto: alto · `Home.razor` (~355, 371, 479, 529, 50)**

El textarea de descripción y los campos de Ticket ID usan `@bind:event="oninput"`. Como toda
la app es **un único componente**, cada pulsación viaja por el WebSocket y repinta el árbol
entero: board, datalist de companies y hilo de notas.

El síntoma agudo no es la CPU. Con `@bind` + `oninput`, Blazor Server reescribe el atributo
`value` desde el estado del servidor; con latencia móvil eso produce el clásico **caracteres
perdidos y cursor que salta** al teclear rápido. Es la acción que más se repite al día.

**Qué hacer.** Quitar `@bind:event="oninput"` de:
- el input Ticket ID (~355) y el textarea Descripción (~371) del modo sencillo,
- sus equivalentes del modo múltiple (~479, ~529),
- `companyQuery` (~50) — solo se lee al pulsar Filtrar.

**Conservar `oninput` en `keyword` (~61)**, que sí alimenta `FilteredTickets` en vivo.

El `@bind` por defecto (`onchange`, al salir del campo) basta: esos valores solo se leen al
pulsar Registrar.

> **Ojo:** `companyQuery` sin `oninput` hace que el aviso de la línea ~73 se actualice al
> salir del campo, no al teclear. Es aceptable; si molesta, deja ese solo con `oninput`.

> **Depende de esto:** T7. Si el borrador se guardara desde un `onchange` de C#, con este
> cambio no se escribiría hasta el blur. Por eso T7 usa JS puro.

**Verificar:** teclear una descripción larga desde el móvil no pierde caracteres ni mueve el
cursor. El valor sigue llegando bien a ConnectWise al registrar.

---

### T2 · Paralelizar las dos consultas del hilo y pintar antes de ITGlue
**Esfuerzo: bajo · Impacto: alto · `ConnectWiseService.GetTicketThreadAsync`, `Home.razor:SelectTicket` (~1012)**

Abrir un ticket encadena **4 llamadas HTTPS en serie**: las notas del ticket, las notas de
entradas de tiempo (independientes entre sí, pero esperadas una tras otra), y luego las dos
de ITGlue. No hay ni un `StateHasChanged()` en medio, así que las notas ya están en memoria
pero siguen ocultas hasta que ITGlue termina.

**Peor aún, y esto es lo que hay que arreglar sí o sí:** mientras la consulta a ITGlue está
en marcha, `isLoadingContext` sigue en `false` y `ticketContext` en `null`, así que el bloque
de ITGlue cae en la rama de «Sin coincidencias en ITGlue.» **y muestra un negativo que es
falso.** El consultor lee que no hay nada relacionado cuando aún se está buscando.

**Qué hacer.**
1. En `GetTicketThreadAsync`, lanzar `GetTicketNotesAsync` y `GetTimeEntryNotesAsync` con
   `Task.WhenAll`, **conservando el `try/catch` best-effort** de las entradas de tiempo (si
   fallan, el hilo debe seguir mostrando las notas del ticket).
2. En `SelectTicket`, poner `isLoadingContext = true` **antes** de `await ReloadThread()`, no
   después, para que nunca se pinte el «sin coincidencias» durante la carga.
3. Llamar a `StateHasChanged()` justo después de `await ReloadThread()`, para que el hilo
   aparezca sin esperar a ITGlue.

**Verificar:** al abrir un ticket, las notas aparecen claramente antes que el bloque de
ITGlue, y en ningún momento se lee «Sin coincidencias» mientras el spinner está activo.

---

### T3 · Subir los campos de formulario a 16px para que iOS deje de hacer zoom
**Esfuerzo: bajo · Impacto: alto · `app.css` (~239)**

`.cw-input, .cw-select, .cw-textarea` usan `font-size: 14px`. Safari en iOS **hace zoom
automático** al enfocar cualquier campo por debajo de 16px, y luego el usuario tiene que
volver a alejar a mano. Pasa en cada campo, cada vez.

**Qué hacer.** Subir a `font-size: 16px` **sin condición** en la regla base (~239).

> **No lo metas dentro de `@media (max-width: 600px)`:** el iPhone en apaisado mide 844px de
> ancho y se quedaría fuera del media query, con el bug intacto.
>
> **No uses `maximum-scale=1` ni `user-scalable=no`** en el viewport: eso mata el pellizco
> para ampliar y rompe la accesibilidad. Subir el tamaño de fuente es la única solución limpia.

En escritorio la diferencia visual es mínima.

**Verificar:** en un iPhone real, tocar Descripción o Ticket ID no cambia el zoom de la página.

---

### T4 · Arreglar el contraste de la paleta en los dos temas
**Esfuerzo: bajo · Impacto: alto · `app.css` (~5-52, 977-978)**

Ratios WCAG calculados sobre los hex reales:

| Dónde | Ahora | Mínimo AA |
|---|---|---|
| `--text-muted #6366f1` sobre `--surface-variant #eef2ff` (claro) | **3.99:1** | 4.5:1 |
| `--primary #2563eb` sobre `--surface-variant #16213e` (oscuro) | **3.08:1** | 4.5:1 |
| `.field-label` sobre blanco | **4.47:1** | 4.5:1 |

Afecta al nombre de la company de cada fila del board, a la fecha de cada nota, al contador
«X / Y» del board y —lo más grave— a **`.btn-cancel`, que es la etiqueta de un botón** del
modal de configuración.

La causa de fondo en tema oscuro: `--primary`, `--success`, `--error` y `--warning` se
declaran en `:root` (~5-10) **fuera de todo tema**, y el bloque `.dark` (~33-52) no los
redefine.

**Qué hacer.**
1. En `:root`: `--text-muted: #4f46e5` → 5.62:1 sobre `#eef2ff` y 6.29:1 sobre blanco.
2. Añadir al bloque `.dark`: `--primary: #60a5fa;` y `--error: #f87171;` → 6.25:1 y 5.84:1
   sobre `#16213e`.
3. Oscurecer los badges de tipo de nota (~977-978) a `#b45309` y `#15803d` → 5.02:1 con
   texto blanco.

**Verificar:** recalcula los ratios con los hex nuevos antes de dar la tarea por hecha, y
revisa el resultado **en los dos temas** (el botón de la luna en la barra superior).

---

### T5 · Guard contra reenvío y no reenviar lo que ya se registró
**Esfuerzo: bajo (a) / medio (b) · Impacto: alto · `Home.razor` (~1160, ~1220, ~1271)**

Hay **dos problemas distintos**, y el segundo es el grave.

**(a) Doble toque.** `SubmitEntry` y `SubmitMultiEntry` no arrancan con un guard. La única
protección es `disabled="@isSubmitting"` en el botón, que solo llega al navegador tras un
ida y vuelta por el WebSocket. La ventana de carrera es estrecha (~1 round-trip) pero existe.

*Arreglo:* primera línea de ambos métodos, `if (isSubmitting) return;`. Dos líneas.

**(b) Reintento tras éxito parcial — este duplica horas de verdad.** El formulario solo se
limpia cuando `successCount > 0 && errorCount == 0`. Si mandas 5 entradas y 3 funcionan,
sale «Parcial: 3 exitosas, 2 errores» y **las 5 filas siguen en pantalla**. El consultor
corrige las 2 que fallaron, pulsa Registrar otra vez… y **las 3 que ya habían entrado se
vuelven a enviar**. Horas duplicadas en ConnectWise, y nadie se entera hasta que alguien
revisa la facturación.

*Arreglo:* dar a `DateEntry` y `MultiTicketEntry` un campo `Posted` (bool) y `LastError`
(string?). El `foreach` salta las filas con `Posted == true`; el error se pinta junto a la
fila que falló. Así el reintento manda solo lo que falta y se ve exactamente qué queda
pendiente.

**Verificar:** simula un fallo parcial (un Ticket ID inexistente entre varios válidos),
pulsa Registrar dos veces, y comprueba en ConnectWise que **no hay entradas duplicadas**.

---

### T6 · Que el prerender no pinte el modal de Configuración vacío
**Esfuerzo: medio · Impacto: alto · `Home.razor` (~2, ~898-914)**

`Home.razor` declara `@rendermode InteractiveServer` **sin `prerender: false`**, así que
`OnInitializedAsync` se ejecuta primero en el prerender, donde **no hay JS interop**.
`ConfigStorageService.GetItemAsync` traga la excepción y devuelve `null` en las 9 claves →
`config.IsComplete()` da `false` → se llama a `OpenSettings()`.

Resultado: **el HTML que se sirve ya trae el modal «⚙️ Configuración» con los campos en
blanco**, y se queda ahí hasta que engancha el circuito (~1 s, más si Render venía dormido).
Ocurre **en cada apertura de la PWA**, y da la impresión de que se borraron las llaves.

**Qué hacer.** Que la fase de prerender no pinte estado de negocio:

```csharp
if (!RendererInfo.IsInteractive) { isBooting = true; return; }   // RendererInfo existe en .NET 9
```

y mover la carga de config + `OpenSettings()` a la primera pasada interactiva (por ejemplo
`OnAfterRenderAsync(firstRender)`, que ya se usa para el foco del visor de imágenes).
Mientras `isBooting`, pintar un «Conectando…» con spinner en lugar del modal.

**Verificar:** recargar con las credenciales ya guardadas no debe mostrar ni un parpadeo del
modal de configuración. Y con `localStorage` vacío, el modal **sí** debe abrirse.

---

### T7 · Borrador del formulario en `localStorage`
**Esfuerzo: medio · Impacto: alto · `Home.razor` (~738-768), `download.js`**

Todo el trabajo en curso vive solo en el circuito: `description`, `dateEntries`,
`multiTickets`. Cuando el circuito muere se pierde. **Y muere a menudo:** Render duerme la
instancia a los ~15 min, pero sobre todo **mandar la PWA a segundo plano o bloquear la
pantalla del iPhone corta el WebSocket**, y pasado el tiempo de retención del circuito (3 min
por defecto) el estado ya no existe. Eso pasa a diario. No es lentitud: es trabajo perdido.

**Qué hacer.** Guardar un borrador (`description`, `ticketId`, `isMultiMode`, `dateEntries`,
`multiTickets`, `selectedTicketId`) bajo una clave `cw_draft`, y restaurarlo al arrancar.

> **Detalle de implementación que importa:** **no** cuelgues el guardado de un `onchange` de
> C#. Una vez muerto el circuito ya no puedes hacer JS interop, y con T1 aplicado `onchange`
> solo dispara al salir del campo. **Espeja el textarea a `localStorage` desde JS puro en el
> evento `input`** (unas líneas en `download.js`, cero tráfico por el circuito) y lee esa
> clave al iniciar.

Complemento barato: un `<div id="components-reconnect-modal">` propio en `App.razor` con
texto en español («El servidor se durmió. Recarga la página; tus entradas ya enviadas están
en ConnectWise») en vez del overlay genérico en inglés del framework.

> Corrección a un borrador anterior de esta tarea: el `sessionLog` **no** es el único registro
> de lo enviado — lo registrado está en ConnectWise y el propio hilo lo muestra. La pérdida
> que no tiene sustituto es **el formulario a medio escribir**. Prioriza eso.

**Verificar:** escribe media entrada, mata el proceso del servidor, arráncalo, recarga: el
texto y las filas deben volver.

---

### T8 · El `.xlsx` del board exporta lo que NO estás viendo
**Esfuerzo: bajo · Impacto: alto · `Home.razor` (~1054-1056, ~936)**

La exportación usa `myTickets`, pero lo que ves en pantalla es `FilteredTickets`. El propio
contador de la cabecera admite que son dos cosas distintas (`@FilteredTickets.Count() / @myTickets.Count`),
y el texto junto al botón **promete lo contrario**: «Sin selección → se incluyen todos los
tickets **listados**».

Escribes «backup» en palabra clave, ves 4 tickets, pulsas Descargar y **te bajas 28 filas**,
con un snackbar que confirma «✓ Reporte generado (28 tickets)». Como el reporte es lo que se
manda fuera, el fallo se descubre cuando ya está enviado.

Segundo orden del mismo bug: `selectedForReport` nunca se poda cuando `LoadMyTickets`
reemplaza la lista. Marcas 3 tickets, cambias de company, y el contador sigue diciendo
«3 ticket(s) seleccionados» mientras el reporte puede salir con 1 fila — o abortarse con
«No hay tickets para exportar» con el board lleno.

**Qué hacer.** Exportar sobre `FilteredTickets`, y podar `selectedForReport` a los ids
presentes tras cada `LoadMyTickets`.

**Verificar:** filtra por palabra clave, exporta, y cuenta que las filas del `.xlsx`
coinciden con lo que hay en pantalla.

---

### T9 · «Palabra clave» significa dos cosas distintas y no se puede deshacer
**Esfuerzo: medio · Impacto: alto · `Home.razor` (~881-889, ~936), `ConnectWiseService` (~100)**

Mientras escribes, el campo filtra **en cliente** sobre cinco campos: título, nombre de
company, identifier, estado e id. Pero al pulsar Filtrar, ese mismo texto se manda al
servidor como una condición **mucho más estrecha**: solo el título (`summary contains`).

El consultor escribe el nombre de un cliente para acotar, ve sus tickets (coinciden por
`Company.Name`), pulsa Filtrar para aplicar la company… **y desaparecen**, porque el título
no contiene esa palabra.

Y no hay vuelta atrás: `myTickets` ya es la respuesta estrecha del servidor, así que borrar
el texto no restaura nada. Hay que volver a pulsar Filtrar —otra ida y vuelta a ConnectWise—
para recuperar lo que ya se había traído. Es además el único filtro **sin botón de limpiar**.

**Qué hacer.** Separar los dos conceptos. Lo más simple y honesto: que «Palabra clave» sea
**solo filtro en cliente** (no se manda al servidor) y añadir, si hace falta, un botón aparte
«Buscar en ConnectWise» que sí haga la consulta por título, con su etiqueta diciéndolo. Y en
cualquier caso, añadir una «✕» para limpiar el campo.

**Verificar:** escribir el nombre de una company y pulsar Filtrar ya no vacía la lista.

---

### T10 · Que el hilo se vea al tocar un ticket en el móvil
**Esfuerzo: bajo · Impacto: alto · `Home.razor` (~152), `download.js`**

Por debajo de 1000px el grid colapsa a una columna y `col-right` (el detalle) queda **entero
por debajo** de la tarjeta del board. `SelectTicket` no hace ningún scroll — no hay una sola
llamada a `scrollIntoView` en todo el repo. En un iPhone, tocar un ticket pinta el hilo una
pantalla más abajo.

> Matiz: el toque **sí** se acusa visualmente (`.ticket-item.selected` pinta borde y sombra),
> así que no es que «no pase nada»; lo que queda fuera de pantalla es el contenido.

**Qué hacer — solo esto:** `@ref` en la tarjeta de detalle y un
`window.cwScrollTo = el => el.scrollIntoView({behavior:'smooth', block:'start'})` en
`download.js`, invocado al final de `SelectTicket` cuando `selectedTicketId is not null`.

> **No** reordenes las columnas con `display: contents` + `order: -1`. Es más frágil y rompe
> el orden de lectura.

**Verificar:** en un iPhone, tocar un ticket lleva el hilo a la vista solo.

---

### T11 · Alturas fijas que desperdician la pantalla grande y estorban en el móvil
**Esfuerzo: bajo · Impacto: medio · `app.css` (~829, ~946)**

`.tickets-list` tiene `max-height: 340px` y `.notes-thread` `max-height: 360px`, ambos en
píxeles fijos y sin relajarse en móvil. Medido a 1920×1080 con 28 tickets: **caben 3,2
tickets** de 28, y la columna izquierda queda vacía desde y=766 hasta abajo.

**Qué hacer.** Cambiar los dos `max-height` fijos por altura relativa al viewport, y hacer
la columna izquierda pegajosa (`position: sticky; top: 76px`) para que el board siga visible
mientras se escribe a la derecha.

> **Dos avisos que salieron de la verificación:**
> - **No pongas `max-height: none` en `.tickets-list` en móvil.** El board lista hasta 100
>   tickets; sin tope empujaría el detalle y el formulario miles de píxeles hacia abajo y
>   **agravaría T10**.
> - **Usa `svh`, no `vh`.** En Safari iOS `100vh` es el viewport grande y mete contenido bajo
>   la barra del navegador.
>
> **No toques `max-width: 1340px`** (~146). Ensancharlo alargaría las líneas del textarea y
> de las notas hasta hacerlas incómodas de leer. Es una decisión de legibilidad, no un descuido.

---

### T12 · Recordar filtros, tema y selección entre recargas
**Esfuerzo: medio · Impacto: medio · `Home.razor` (~898), `ConfigStorageService`**

`OnInitializedAsync` nunca llama a `LoadMyTickets()`: la lista arranca vacía y hay que pulsar
«📥 Cargar mis tickets» cada vez. Y `isDark`, `companyQuery`, `keyword`, `includeUnassigned`
y `selectedTicketId` son campos de instancia sin persistir.

`ConfigStorageService` ya tiene los helpers `GetItemAsync`/`SetItemAsync`; solo hay que añadir
claves de UI (`cw_ui_dark`, `cw_ui_company`, `cw_ui_keyword`, `cw_ui_unassigned`,
`cw_ui_selected`), leerlas al iniciar y escribirlas al cambiar.

Si `config.IsComplete()`, cargar los tickets automáticamente al arrancar.

> **Hazlo después de T6**, o la carga automática se disparará también en el prerender.

---

### T13 · Mostrar en la lista lo que el `.xlsx` ya muestra
**Esfuerzo: bajo · Impacto: medio · `Home.razor` (~122), `ConnectWiseService` (~104)**

`TicketSummary` ya trae `Board`, `Priority` e `Info.LastUpdated`, y `GetTicketsAsync` hasta
convierte `LastUpdated` a la zona del usuario. El `.xlsx` los pinta los tres. **La lista en
pantalla no muestra ninguno.** El mapeo y el formateo ya están escritos; solo falta pintarlos.

Además la consulta ordena por `id desc` — el ticket **más nuevo**, no el que **se movió hace
poco**, que es lo que suele interesar.

**Qué hacer.** Añadir una tercera línea al `.ticket-item` con prioridad y última actividad,
resaltando lo actualizado hoy. Y ordenar por última actualización (en la consulta o en cliente).

---

### T14 · Foco visible al navegar con teclado
**Esfuerzo: bajo · Impacto: medio · `app.css` (~241, ~245-248)**

Los campos hacen `outline: none` y lo sustituyen por un halo de
`rgba(99,102,241,0.15)` que da **1.21:1** contra el fondo — invisible. El único indicio real
es el cambio de borde: **2.99:1**, justo por debajo del 3:1 que exige WCAG SC 1.4.11.

**Qué hacer:**

```css
.cw-input:focus-visible, .cw-select:focus-visible, .cw-textarea:focus-visible {
    outline: 2px solid var(--input-focus);
    outline-offset: 1px;
}
```

y subir el halo a `0.35`. Dos líneas de CSS, sin tocar el `.razor`.

---

### T15 · El filtro por company falla en silencio
**Esfuerzo: bajo · Impacto: medio · `Home.razor` (~930, ~953-965)**

Si el texto de company no coincide **exacto** con un nombre de la lista, `companyId` queda
`null` y la consulta **sale sin filtro de company** — sin avisar. Como el keyword sí se
aplica en servidor, la lista cambia y **parece** filtrada, pero no por el cliente que la caja
sigue mostrando. Y si el `GET` de companies falló (llaves malas, contenedor frío), la lista
está vacía, **ningún** texto hace match, y el filtro por cliente deja de existir en silencio.

**Qué hacer.** Si `companyQuery` no está vacío y no hay match exacto, **no llamar a la API**:
avisar («No encuentro la company "Acme". Elige una de la lista»). Y guardar el mensaje del
`catch` de `LoadCompanies` en un campo `companiesError` en vez de descartarlo, para poder
decir «No se pudo cargar la lista de companies».

---

### T16 · Sumar horas
**Esfuerzo: medio · Impacto: medio · `Home.razor`**

La app registra horas pero **no suma horas en ningún sitio**: ni las que estás a punto de
cargar (en modo múltiple, varias filas × varios tickets), ni las que ya tiene el ticket.

Mostrar el total del formulario antes de enviar es barato y evita cargar de más. El total ya
registrado en el ticket se puede sacar de las entradas de tiempo que **ya se están leyendo**
para el hilo (`GetTimeEntryNotesAsync`); haría falta llevar `ActualHours` al modelo.

---

### T17 · `viewport-fit=cover` — hazlo el último y con cuidado
**Esfuerzo: bajo · Impacto: bajo-medio · `App.razor` (~6)**

El viewport no declara `viewport-fit=cover`, pero `App.razor` **sí** declara
`apple-mobile-web-app-status-bar-style: black-translucent`. Sin `viewport-fit=cover`,
`env(safe-area-inset-*)` resuelve a **0**, así que las dos defensas escritas en el visor de
imágenes (`padding-top` y `padding-bottom` con `env()`) son **hoy código muerto**.

> **Aviso de la verificación:** añadir `viewport-fit=cover` es precisamente lo que hace que el
> contenido pase a dibujarse **a sangre bajo el notch**. Si lo añades, tienes que añadir en la
> misma pasada el `padding-top: env(safe-area-inset-top)` a la barra superior y el
> `padding-bottom` al contenedor principal y al snackbar, o **empeorarás** lo que hay.
>
> La parte de la barra superior es **predicción, no observación**: si el solapamiento fuera
> grave, los consultores ya se habrían quejado. Trátalo como mejora opcional y **compruébalo
> en un iPhone real antes y después**, no como tarea a ciegas.

---

## 5. Descartado — no lo vuelvas a proponer

Dos hallazgos se cayeron en la verificación. **Los hechos del código eran exactos**, pero se
descartaron por relevancia para este proyecto concreto (herramienta interna, 2-3 consultores
conocidos, sin usuarios de teclado ni lector de pantalla declarados):

- **Las filas del board son `<div @onclick>` sin `tabindex` ni `role`,** así que no se pueden
  abrir con teclado.
- **Ninguno de los 21 campos tiene `<label for=...>` asociado** (solo hay un `id` en todo el
  `.razor`, el del `datalist`).

Si algún día la usa alguien con lector de pantalla o por teclado, **estos dos vuelven a la
lista y suben a prioridad alta**. Ambos son de esfuerzo bajo.

---

## 6. Riesgo abierto que hay que confirmar en producción

**Las imágenes de las notas nunca se probaron contra ConnectWise real.** No sabemos si
`/v4_6_release/api/newinlineimages/…` acepta **Basic auth con las API keys** o exige cookie
de sesión del web app.

El código degrada solo: si la descarga falla, `window.cwNoteImageFailed` cambia la miniatura
por un enlace «🖼 Ver imagen en ConnectWise». **Así que un fallo se ve como el enlace de
respaldo, no como un error.**

**Primera comprobación al retomar:** abrir un ticket con una imagen en el hilo. Si aparece el
enlace en vez de la imagen, esa ruta no acepta las API keys y hay que buscar el endpoint
correcto (probablemente el de attachments del API 3.0, no el del web app).
