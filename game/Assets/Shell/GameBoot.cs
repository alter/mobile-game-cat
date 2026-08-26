using CatShelter.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.Shell
{
    /// <summary>
    /// Builds the entire scene from code at startup — per cat-shelter-tech.md:
    /// "scenes assembled by code — so an agent never has to touch scene YAML".
    /// The only thing the scene asset needs is this one component.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameBoot : MonoBehaviour
    {
        private void Awake()
        {
            // Analytics sink: no-op until the GameAnalytics SDK task wires the
            // real one. Configure(null, null) keeps calls valid and silent.
            Core.Analytics.Configure(null, null);

            // Does nothing unless a `visiontest` folder was pushed into the
            // app container; see VisionSelfTest.
            VisionSelfTest.RunIfRequested();

            // Opening the game today means no reminder about today: the next
            // one moves to tomorrow evening.
            EveningReminder.Reschedule();
            StartCoroutine(EveningReminder.DebugRequestNow(this));
        }

        private void OnEnable()
        {
            var uid = GetComponent<UIDocument>();
            if (uid.rootVisualElement == null)
            {
                // No UXML assigned: build a bare root so DebugGameView can
                // populate it. Everything is code-generated.
                uid.visualTreeAsset = null;
                var root = uid.rootVisualElement;
                root?.Clear();
            }
            if (GetComponent<DebugGameView>() == null)
                gameObject.AddComponent<DebugGameView>();
        }
    }
}
