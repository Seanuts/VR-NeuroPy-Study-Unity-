using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enables Meta Quest passthrough for the mixed modes without requiring a
/// compile-time reference to the optional Meta/Oculus SDK assemblies.
/// </summary>
public static class QuestProPassthrough
{
    struct LayerState
    {
        public GameObject gameObject;
        public int layer;
    }

    struct CameraState
    {
        public Camera camera;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public int cullingMask;
    }

    struct GraphicState
    {
        public Graphic graphic;
        public bool enabled;
    }

    static readonly List<LayerState> layerStates = new List<LayerState>();
    static readonly List<CameraState> cameraStates = new List<CameraState>();
    static readonly List<GraphicState> graphicStates = new List<GraphicState>();
    static readonly HashSet<GameObject> layerStateObjects = new HashSet<GameObject>();
    static Behaviour passthroughLayer;
    static Component passthroughManager;
    static Camera simulatedCamera;
    static Transform simulatedPlayerTransform;
    static int isolatedLayer = -1;
    static bool isEnabled;
    static bool hasLoggedUnavailable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Camera.onPreRender -= ForceTransparentCamera;
        layerStates.Clear();
        cameraStates.Clear();
        graphicStates.Clear();
        layerStateObjects.Clear();
        passthroughLayer = null;
        passthroughManager = null;
        simulatedCamera = null;
        simulatedPlayerTransform = null;
        isolatedLayer = -1;
        isEnabled = false;
        hasLoggedUnavailable = false;
    }

    public static void SetEnabled(
        bool enabled,
        GameObject xrPlayer,
        GameObject simulatedPlayer,
        GameObject tvScreen,
        GameObject activeSlideshow,
        bool slideshowOnlyInSimulatedView)
    {
        if (!enabled)
        {
            // Virtual modes own an explicit opaque state. Do not restore the
            // scene's serialized passthrough state here: MainScene may contain
            // an enabled manager/layer so mixed modes can opt into it later.
            Disable();
            return;
        }

        if (isEnabled)
        {
            // Mixed3D and Mixed2D use different screen paths. Rebuild only
            // the temporary scene isolation while keeping passthrough active.
            RestoreSceneIsolation();
            IsolateWardFromXRCamera(tvScreen, activeSlideshow, slideshowOnlyInSimulatedView);
            ConfigureSceneCameras(simulatedPlayer);
            return;
        }

        Enable(xrPlayer, simulatedPlayer, tvScreen, activeSlideshow, slideshowOnlyInSimulatedView);
    }

    static void Enable(
        GameObject xrPlayer,
        GameObject simulatedPlayer,
        GameObject tvScreen,
        GameObject activeSlideshow,
        bool slideshowOnlyInSimulatedView)
    {
        // Keep the clinical ward renderable for SimulatedPlayer, but isolate
        // it from the XR camera. The TV/slideshow hierarchy stays visible.
        IsolateWardFromXRCamera(tvScreen, activeSlideshow, slideshowOnlyInSimulatedView);
        ConfigureSceneCameras(simulatedPlayer);
        Camera.onPreRender += ForceTransparentCamera;
        isEnabled = true;

        Type managerType = FindType("OVRManager");
        Type layerType = FindType("OVRPassthroughLayer");
        if (managerType == null || layerType == null)
        {
            LogUnavailableOnce();
            return;
        }

        passthroughManager = FindComponent(managerType);
        if (passthroughManager == null)
        {
            LogUnavailableOnce();
            return;
        }

        if (!SetMemberValue(passthroughManager, "isInsightPassthroughEnabled", true))
        {
            LogUnavailableOnce();
            passthroughManager = null;
            return;
        }

        passthroughLayer = FindComponent(layerType) as Behaviour;
        if (passthroughLayer == null)
        {
            GameObject layerHost = FindCameraRigObject(xrPlayer);
            if (layerHost == null)
                layerHost = FindCameraRigObject(null);
            if (layerHost == null)
                layerHost = xrPlayer;

            if (layerHost != null)
                passthroughLayer = layerHost.AddComponent(layerType) as Behaviour;
        }

        if (passthroughLayer == null)
        {
            SetMemberValue(passthroughManager, "isInsightPassthroughEnabled", false);
            passthroughManager = null;
            LogUnavailableOnce();
            return;
        }

        SetMemberEnumValue(passthroughLayer, "overlayType", "Underlay");
        SetMemberValue(passthroughLayer, "hidden", false);
        passthroughLayer.gameObject.SetActive(true);
        passthroughLayer.enabled = true;

    }

    static void Disable()
    {
        Camera.onPreRender -= ForceTransparentCamera;
        isEnabled = false;

        RestoreSceneIsolation();

        // Disable every layer rather than only the one used by this helper.
        // A transparent material (for example cupboard glass) can reveal any
        // active passthrough underlay even when the rest of the ward renders.
        SetAllPassthroughLayersEnabled(false);
        SetAllPassthroughManagersEnabled(false);

        passthroughLayer = null;
        passthroughManager = null;
    }

    static void SetAllPassthroughLayersEnabled(bool enabled)
    {
        Type layerType = FindType("OVRPassthroughLayer");
        if (layerType == null)
            return;

        foreach (Component component in FindComponents(layerType))
        {
            Behaviour layer = component as Behaviour;
            if (layer == null)
                continue;

            SetMemberValue(layer, "hidden", !enabled);
            layer.enabled = enabled;
        }
    }

    static void SetAllPassthroughManagersEnabled(bool enabled)
    {
        Type managerType = FindType("OVRManager");
        if (managerType == null)
            return;

        foreach (Component manager in FindComponents(managerType))
            SetMemberValue(manager, "isInsightPassthroughEnabled", enabled);
    }

    static void RestoreSceneIsolation()
    {

        for (int i = 0; i < layerStates.Count; i++)
        {
            LayerState state = layerStates[i];
            if (state.gameObject != null)
                state.gameObject.layer = state.layer;
        }
        layerStates.Clear();
        layerStateObjects.Clear();

        for (int i = 0; i < graphicStates.Count; i++)
        {
            GraphicState state = graphicStates[i];
            if (state.graphic != null)
                state.graphic.enabled = state.enabled;
        }
        graphicStates.Clear();

        for (int i = 0; i < cameraStates.Count; i++)
        {
            CameraState state = cameraStates[i];
            if (state.camera != null)
            {
                state.camera.clearFlags = state.clearFlags;
                state.camera.backgroundColor = state.backgroundColor;
                state.camera.cullingMask = state.cullingMask;
            }
        }
        cameraStates.Clear();

        simulatedCamera = null;
        simulatedPlayerTransform = null;
        isolatedLayer = -1;
    }

    static void IsolateWardFromXRCamera(
        GameObject tvScreen,
        GameObject activeSlideshow,
        bool slideshowOnlyInSimulatedView)
    {
        Transform screenTransform = tvScreen != null ? tvScreen.transform : null;
        Transform slideshowTransform = activeSlideshow != null ? activeSlideshow.transform : null;
        isolatedLayer = FindUnusedLayer();

        // Keep Mixed3D slides available to SimulatedPlayer, but prevent their
        // world-space Canvas from appearing directly in the XR view.
        if (slideshowOnlyInSimulatedView && slideshowTransform != null &&
            !IsInHierarchy(slideshowTransform, screenTransform) &&
            !IsInHierarchy(screenTransform, slideshowTransform))
        {
            Canvas slideshowCanvas = slideshowTransform.GetComponentInParent<Canvas>(true);
            if (slideshowCanvas != null)
                MoveObjectToIsolatedLayer(slideshowCanvas.gameObject);
        }

        Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            bool belongsToScreen = IsInHierarchy(renderer.transform, screenTransform) ||
                                   (!slideshowOnlyInSimulatedView &&
                                    IsInHierarchy(renderer.transform, slideshowTransform));
            if (!belongsToScreen)
            {
                MoveObjectToIsolatedLayer(renderer.gameObject);
            }
        }

        Graphic[] graphics = UnityEngine.Object.FindObjectsOfType<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
                continue;

            graphicStates.Add(new GraphicState
            {
                graphic = graphic,
                enabled = graphic.enabled
            });

            bool belongsToScreen = IsInHierarchy(graphic.transform, screenTransform) ||
                                   (!slideshowOnlyInSimulatedView &&
                                    IsInHierarchy(graphic.transform, slideshowTransform));
            bool belongsToSimulatedOnly = slideshowOnlyInSimulatedView &&
                                           IsInHierarchy(graphic.transform, slideshowTransform) &&
                                           !IsInHierarchy(graphic.transform, screenTransform);
            bool belongsToTransitionFade = graphic.GetComponentInParent<LearningBlockFade>(true) != null;
            if (!belongsToScreen && !belongsToSimulatedOnly && !belongsToTransitionFade)
                graphic.enabled = false;
        }
    }

    static void MoveObjectToIsolatedLayer(GameObject gameObject)
    {
        if (gameObject == null || !layerStateObjects.Add(gameObject))
            return;

        layerStates.Add(new LayerState
        {
            gameObject = gameObject,
            layer = gameObject.layer
        });
        gameObject.layer = isolatedLayer;
    }

    static void ConfigureSceneCameras(GameObject simulatedPlayer)
    {
        simulatedCamera = FindSimulatedCamera(simulatedPlayer);
        simulatedPlayerTransform = simulatedPlayer != null ? simulatedPlayer.transform : null;
        int isolatedLayerMask = 1 << isolatedLayer;

        Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);

        foreach (Camera camera in cameras)
        {
            if (camera == null)
                continue;

            cameraStates.Add(new CameraState
            {
                camera = camera,
                clearFlags = camera.clearFlags,
                backgroundColor = camera.backgroundColor,
                cullingMask = camera.cullingMask
            });

            if (IsSimulatedCamera(camera))
            {
                // The simulated-player camera must keep the ward visible so
                // it can render it into the TV screen.
                camera.cullingMask |= isolatedLayerMask;
            }
            else
            {
                camera.cullingMask &= ~isolatedLayerMask;
                camera.clearFlags = CameraClearFlags.SolidColor;
                // Passthrough needs an actually clear eye buffer. Retaining
                // Unity's default blue RGB values with alpha zero can still
                // leak a blue cast through device-side compositing.
                camera.backgroundColor = Color.clear;
            }
        }
    }

    static void ForceTransparentCamera(Camera camera)
    {
        if (!isEnabled || camera == null || IsSimulatedCamera(camera))
            return;

        camera.clearFlags = CameraClearFlags.SolidColor;
        if (isolatedLayer >= 0)
            camera.cullingMask &= ~(1 << isolatedLayer);
        camera.backgroundColor = Color.clear;
    }

    static Camera FindSimulatedCamera(GameObject simulatedPlayer)
    {
        if (SimulatedPlayer.ActiveSimulatedCamera != null)
            return SimulatedPlayer.ActiveSimulatedCamera;

        if (simulatedPlayer == null)
            return null;

        foreach (Camera camera in simulatedPlayer.GetComponentsInChildren<Camera>(true))
        {
            if (camera != null)
                return camera;
        }

        return null;
    }

    static bool IsSimulatedCamera(Camera camera)
    {
        if (camera == null)
            return false;

        return camera == simulatedCamera ||
               (simulatedPlayerTransform != null &&
                camera.transform.IsChildOf(simulatedPlayerTransform));
    }

    static int FindUnusedLayer()
    {
        bool[] usedLayers = new bool[32];
        foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
        {
            if (transform != null && transform.gameObject.layer >= 0 && transform.gameObject.layer < 32)
                usedLayers[transform.gameObject.layer] = true;
        }

        for (int layer = 31; layer >= 8; layer--)
        {
            if (!usedLayers[layer])
                return layer;
        }

        // All user layers are occupied. Layer 31 is restored on exit.
        return 31;
    }

    static GameObject FindCameraRigObject(GameObject xrPlayer)
    {
        MonoBehaviour[] behaviours = xrPlayer != null
            ? xrPlayer.GetComponentsInChildren<MonoBehaviour>(true)
            : UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null && behaviour.GetType().Name == "OVRCameraRig")
                return behaviour.gameObject;
        }

        return null;
    }

    static Component FindComponent(Type componentType)
    {
        foreach (Component component in FindComponents(componentType))
            return component;

        return null;
    }

    static IEnumerable<Component> FindComponents(Type componentType)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (behaviour != null && componentType.IsAssignableFrom(behaviour.GetType()))
                yield return behaviour;
        }
    }

    static bool IsInHierarchy(Transform child, Transform possibleParent)
    {
        if (child == null || possibleParent == null)
            return false;

        return child == possibleParent || child.IsChildOf(possibleParent);
    }

    static Type FindType(string typeName)
    {
        Type type = Type.GetType(typeName);
        if (type != null)
            return type;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
                return type;

            try
            {
                foreach (Type candidate in assembly.GetTypes())
                {
                    if (candidate.Name == typeName)
                        return candidate;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // An optional SDK assembly may not be fully loadable on desktop.
            }
        }

        return null;
    }

    static bool SetMemberValue(object target, string memberName, object value)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return true;
        }

        FieldInfo field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
            return true;
        }

        return false;
    }

    static bool SetMemberEnumValue(object target, string memberName, string enumName)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Type enumType = property != null ? property.PropertyType : null;
        if (enumType == null || !enumType.IsEnum)
        {
            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            enumType = field != null ? field.FieldType : null;
        }

        if (enumType == null || !enumType.IsEnum)
            return false;

        try
        {
            object enumValue = Enum.Parse(enumType, enumName);
            return SetMemberValue(target, memberName, enumValue);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    static void LogUnavailableOnce()
    {
        if (hasLoggedUnavailable)
            return;

        hasLoggedUnavailable = true;
        Debug.LogWarning(
            "Quest Pro passthrough is unavailable. Add the Meta/Oculus passthrough SDK before building for Quest.");
    }
}
