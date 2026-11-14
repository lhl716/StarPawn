// Hand-written stand-ins where the mod passes lambdas/events. Shapes are inferred from
// this repository's own call sites, NOT verified against Assembly-CSharp.
using System;
using System.Collections.Generic;
namespace UnityEngine { public partial class __NsAnchor {} }
namespace UnityEngine.UI { public partial class Image {
 public enum Type { Simple, Sliced, Tiled, Filled }
 public Type type; public bool preserveAspect; public bool raycastTarget; public float alphaHitTestMinimumThreshold; public dynamic sprite; public dynamic color; public dynamic rectTransform; public dynamic gameObject; public bool enabled;
} }
public partial class GraphNodeStorage {}
public partial class Tag { public void SetString(string v){} public void SetInt(int v){} public dynamic GetString(){return "";} public dynamic GetInt(){return 0;} }
public partial class GameItem : GraphNodeStorage {
 public Func<GameItem,GameItem,bool> mayStackItemFunc;
 public Func<GameItem,dynamic,bool> mayInventoryRemoveItemFunc;
 public Func<GameItem,dynamic,bool> mayInventoryAddItemFunc;
 public Func<GameItem,dynamic,dynamic,bool> maySelectSlotItemFunc;
 public Func<GameItem,dynamic,dynamic,bool> canSelectSlotItemFunc;
 public Func<GameItem,GameItem,bool> mayThisTargetItemFunc;
 public Func<GameItem,GameItem,bool> canThisTargetItemFunc;
 public Action<GameItem,GameItem> onThisTargetItemFunc;
 public void ModifyTag(string tag, Action<Tag> change){}
 public Tag GetTagReadonly(string tag){return new Tag();}
}
public partial class GameInventory : GraphNodeStorage { public GameInventory(params object[] a){} }
public partial class ModItemDirectory { public void Add(string id, Func<GameItem> create){} }
public static partial class ModHook {
 public static event Action<ModItemDirectory> OnModItemDirectoryInit;
 public static event Action OnGameLoadedInit; public static event Action OnGameLoadedLate;
 public static event Action<string> OnNewGameStarted; public static event Action OnHandlingNightlyServicesLate;
}
public partial class ModManifest { public string ID=""; }
public partial class ModRecord { public ModManifest Manifest=new ModManifest(); public Dictionary<string,dynamic> Textures=new Dictionary<string,dynamic>(); public Dictionary<string,dynamic> PaperSprites=new Dictionary<string,dynamic>(); }
public static partial class ModLoader { public static List<ModRecord> Mods=new List<ModRecord>(); }
public partial class PaperUI {}
public partial class GameItem { public int unitCount; public string identifier=""; public int uniqueId; public dynamic contentWindow; public GameInventory parentInventory; public string name=""; public string shortDescription=""; }
public partial class GameInventory { public List<GameItem> childItems=new List<GameItem>(); }
public partial class EmporiumEntry { public static EmporiumEntry Instance=new EmporiumEntry(); public GameInventory invElement=new GameInventory(), frontInvinvElement=new GameInventory(), backInvinvElement=new GameInventory(); }
public partial class PaperUI { public void AddButton(float x,float y,float w,float h,string label,Action action,string id){} public dynamic Last; public void AddImage(float x,float y,float w,float h,string key,string mode,string id){} public void AddText(params object[] a){} }
public static partial class ModHelper { public static void AddDebugFunction(string mod, dynamic category, string label, Action action){} }
public partial class PlayerStore { public void SaveGame(){} public static PlayerStore Instance=new PlayerStore(); public static bool HasInstance; }
namespace UnityEngine { public partial class Object { public static void DontDestroyOnLoad(object o){} public static void Destroy(object o){} }
public partial class GameObject : Object { public T AddComponent<T>() where T : new() => new T(); }
 public partial class MonoBehaviour : Object {} }
public static partial class ItemDirectory { public static GameItem CreateEmptyItem(string id) => new GameItem(); }
public partial class GameItem { public bool MayTarget(GameItem t)=>true; public bool CanTarget(GameItem t)=>true; public GameItem(params object[] a){} }
public partial class GameItemElement { public string identifier=""; public bool useSpriteFromMod; public string spriteAtlasPath=""; public TreeNodeRender background, fakeBackground; public dynamic rectTransform; public dynamic gameObject; public dynamic transform; public GameItem item; }
public partial class TreeNodeRender { public UnityEngine.UI.Image image; public dynamic rectTransform; public dynamic gameObject; public dynamic transform; }
public static partial class ModHelper { public const string DEBUG_CATEGORY_SPECIAL = "special"; }
public partial class ModRecord { public Dictionary<string,dynamic> Sprites=new Dictionary<string,dynamic>(); }
public partial class UISettings { public static UISettings current; public float gridSize; }
// Exact signature from docs/NATIVE_APPRAISAL_AUDIT_05.txt (read from Assembly-CSharp metadata).
public static partial class ModHelper {
 public static void AddAppraisalItem(string modId, string id, string itemId, int unlockRank, Func<GameItem> createRandom, Func<GameItem, int> getCorrectValue, Func<GameItem, bool> isType, string guideItemId, Func<string> displayName){}
 public static int RemoveAppraisalItems(string modId)=>0; public static int GetAppraiserRank()=>0;
 public static GameItem SpawnItem(string id)=>new GameItem(); public static void SetItemOwnerShip(GameItem i,bool v){} }
public static partial class BookHelper { public static void InitBookItem(GameItem item, string bookId){} }
public partial class PlayerStore { public int GetCash()=>0; }
namespace UnityEngine {
 public partial class Object { public static Object FindObjectOfType(System.Type t)=>null; }
 public partial class Component : Object { public Transform transform; public GameObject gameObject; }
 public partial class Transform : Component { public void SetParent(Transform p,bool w){} public void SetAsFirstSibling(){} public Transform Find(string n)=>null; public int childCount; public Transform GetChild(int i)=>null; }
 public partial class RectTransform : Transform { public Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition; }
 public partial class GameObject { public GameObject(string n, params System.Type[] c){} public Transform transform; public void SetActive(bool v){} }
 public partial struct Vector2 { public Vector2(float x,float y){} public static Vector2 zero; }
 public partial struct Vector3 { public static Vector3 zero; }
 public partial struct Color { public Color(float r,float g,float b,float a){} }
}
