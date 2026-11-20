using AlchemyBar.Brewing;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AlchemyBar.Audio
{
    public static class GameSoundsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            foreach (XROrigin origin in Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure<FootstepAudio>(origin.gameObject);

            foreach (XRGrabInteractable grab in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure<GrabbableAudio>(grab.gameObject);

            foreach (BrewingCauldron cauldron in Object.FindObjectsByType<BrewingCauldron>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure<CauldronAudio>(cauldron.gameObject);

            foreach (XRInteractionManager manager in Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None))
            {
                manager.interactableRegistered -= OnInteractableRegistered;
                manager.interactableRegistered += OnInteractableRegistered;
            }
        }

        private static void OnInteractableRegistered(InteractableRegisteredEventArgs args)
        {
            if (args.interactableObject is XRGrabInteractable grab) Ensure<GrabbableAudio>(grab.gameObject);
        }

        private static void Ensure<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null) go.AddComponent<T>();
        }
    }
}
