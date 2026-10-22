# s&box — API-Fakten für Codegenerierung

s&box ist NICHT Unity, NICHT Godot, NICHT MonoGame. Die API-Fläche ist
eigenständig. Raten ist verboten — bei Unsicherheit nachfragen oder die
echte Signatur aus der lokalen Codebasis nachsehen.

## Namespaces

using Sandbox;          // Basis: Component, GameObject, GameTransform, ...
using Sandbox.UI;       // Razor-UI-Komponenten
using Sandbox.Network;  // Netzwerk-APIs
Verboten: UnityEngine.*, Godot.*, System.IO, System.Net (rohe Sockets),
System.Reflection.Emit. Der Code läuft in einem eingeschränkten .NET-Subset.

## Lifecycle — exakte Namen, exakte Reihenfolge

OnAwake()        einmalig bei Erstellung, SYNCHRON, aber nur wenn das
                 Parent-GameObject bereits enabled ist. Läuft INNERHALB
                 von Components.Create<T>() — also bevor die nächste Zeile
                 des aufrufenden Codes ausgeführt wird.
OnEnabled()      bei jedem Aktivieren
OnStart()        beim ersten aktivierten Frame, garantiert vor dem ersten
                 OnFixedUpdate(). Property-Zuweisungen sind hier gelandet.
OnUpdate()       jeden Frame
OnFixedUpdate()  fester Zeitschritt
OnPreRender()    vor dem Rendern — NICHT auf Dedicated Servern
OnDisabled()     beim Deaktivieren

Häufiger Fehler: KI schreibt Unity-Namen (Start, Update, Awake, Ready,
_Ready, FixedUpdate). Alle falsch. Es gibt kein MonoBehaviour, kein
[SerializeField], keine Coroutinen, kein Invoke().

Wichtig für Spawn-Code: Werte, die per [Property] im Inspector gesetzt
werden, landen NACH OnAwake(). Initialisierung, die diese Werte braucht,
gehört in OnStart(), nicht in OnAwake().
Wichtig: Component.Enabled sagt nur, was die Komponente WILL. Ob sie
tatsächlich aktiv ist, sagt Component.Active — das berücksichtigt
deaktivierte Parents.

## GameObject / Component / Transform

Komponenten HOLEN, nicht erben:
    var c = GameObject.Components.Get<MyComponent>();
    var c = GameObject.Components.GetOrCreate<MyComponent>();
    GameObject.Components.Create<MyComponent>();
    GameObject.Components.Has<MyComponent>();
Transform heißt GameTransform, Property heißt GameObject.WorldPosition
bzw. GameObject.LocalPosition. Nicht .transform, nicht .position.
GameObject ist eine Klasse, kein struct — Referenzen sind nullable,
Null-Checks sind Pflicht.

## Networking (Host-authoritative, Steam-Stack)

Jedes GameObject wird per NetworkSpawn() vernetzt:
    GameObject.NetworkSpawn();             // an alle
    GameObject.NetworkSpawn( connection ); // an einen Client

NetworkMode: Never | Object | Snapshot (default)
  Never    — nie vernetzt
  Object   — als eigenes Objekt an alle Clients gesendet
  Snapshot — Host schickt es als Teil des initialen Scene-Snapshots

Nur der HOST darf Netzwerkobjekte spawnen. Clients müssen den Host bitten,
für sie zu spawnen. Das ist die häufigste Ursache für "Objekt fehlt auf
anderen Clients".

[Sync] auf einer Property repliziert sie vom Owner an alle anderen.
Die Codegenerierung hängt sich in den Setter — Änderungen feuern also
automatisch, man muss nichts manuell senden.
    [Sync] public bool IsAlive { get; set; } = true;
    [Sync( SyncFlags.Query )] public Vector3 ExternalPosition { get; set; }

RPCs — Attribut auf die Methode, die Engine generiert das Plumbing:
    [Rpc.Broadcast]  — läuft auf ALLEN Clients, inklusive Host
    [Rpc.Owner]      — nur beim Owner des Objekts, sonst beim Host
    [Rpc.Host]       — nur auf dem Host
RPC-Argumente: exakt dieselben Typen wie bei [Sync]-Properties.
Statische Methoden können ebenfalls RPCs sein (auf jeder statischen Klasse).
[NetPermission]/[NetPermissionLevel] ist veraltet — nicht mehr verwenden.

Sync/RPC-Typen: Primitive, Vector3, Rotation, GameObject-Referenzen,
Netzwerk-Entities. Keine beliebigen Klassen, keine Delegates, keine
Generics, keine List<CustomClass> ohne Netzwerk-Unterstützung.

## Sound

    Sound.Play( "bing", WorldPosition )          // veraltet
    Sound.FromWorld( "bing", WorldPosition )     // korrekt
Nur der Host oder der ausführende Client hört den Sound — bei
Multiplayer-Sounds gehört der Aufruf in einen [Rpc.Broadcast].

## UI

Razor (.razor) mit Sandbox.UI, nicht HTML, nicht XAML, kein MonoBehaviour-UI.
Panel ist die Basisklasse. UI läuft nur auf Clients, nicht auf Dedicated
Servern — UI-Code und Server-Logik deshalb strikt trennen.

## Erlaubt vs. verboten

VERBOTEN:
  System.IO.File / Directory / Stream      → kein Dateizugriff
  System.Diagnostics.Process               → keine Prozesse
  System.Reflection.Emit                   → keine dynamische IL
  Rohe Sockets, eigene Threads             → Netzwerk nur über Sandbox-APIs
  Environment.Exit
ERLAUBT / ERWARTET:
  Sandbox.* durchgehend
  Standardwert-Typen, LINQ, async/await (mit Vorsicht)
  HttpClient für externe Services (HTTP/WebSocket)

## Icons, Prefabs, Assets

Assets werden über Pfade/Handles referenziert, nicht per Pfad-String in
statischen Klassen. Kein Resources.Load(), kein AssetDatabase.
Model.Load( "models/foo.vmdl" ) ist der Weg, nicht Instantiate(Prefab).
Prefabs (/dev/doc) sind eigene Dateien, die gespawnt werden — nicht
GameObject-Instanziierung wie in Unity.

## Stilregeln für generierten Code

- Kein "// TODO: implement"-Gerüst. Vollständige Methoden.
- Keine erfundenen APIs. Wenn eine Signatur unklar ist: sagen, nicht raten.
- Keine Unity-Muster durchschleusen (Singleton-Manager über Find(),
  zentrale Update-Loops, GameObject.Find-Äquivalente).
- Event-Handler statt Polling in OnUpdate, wo s&box das anbietet.
- [Property]-Felder für alles, was der Editor setzen soll.
- Bei Unklarheit über die API-Version: Referenzprojekt oder lokale
  Codebasis als Quelle nennen, nicht die Dokumentation aus dem Gedächtnis
  zitieren.
