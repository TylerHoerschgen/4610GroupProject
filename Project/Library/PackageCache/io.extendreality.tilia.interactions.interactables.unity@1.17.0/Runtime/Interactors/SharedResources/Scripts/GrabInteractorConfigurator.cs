﻿namespace Tilia.Interactions.Interactables.Interactors
{
    using Malimbe.MemberChangeMethod;
    using Malimbe.MemberClearanceMethod;
    using Malimbe.PropertySerializationAttribute;
    using Malimbe.XmlDocumentationAttribute;
    using System.Collections.Generic;
    using Tilia.Interactions.Interactables.Interactables;
    using UnityEngine;
    using Zinnia.Action;
    using Zinnia.Data.Attribute;
    using Zinnia.Data.Collection.List;
    using Zinnia.Extension;
    using Zinnia.Tracking.Collision;
    using Zinnia.Tracking.Collision.Active;
    using Zinnia.Tracking.Velocity;
    using Zinnia.Utility;

    /// <summary>
    /// Sets up the Interactor Prefab grab settings based on the provided user settings.
    /// </summary>
    public class GrabInteractorConfigurator : MonoBehaviour
    {
        #region Facade Settings
        /// <summary>
        /// The public interface facade.
        /// </summary>
        [Serialized]
        [field: Header("Facade Settings"), DocumentedByXml, Restricted]
        public InteractorFacade Facade { get; protected set; }
        #endregion

        #region Grab Settings
        /// <summary>
        /// The <see cref="BooleanAction"/> that will initiate the Interactor grab mechanism.
        /// </summary>
        [Serialized]
        [field: Header("Grab Settings"), DocumentedByXml, Restricted]
        public BooleanAction GrabAction { get; protected set; }
        /// <summary>
        /// The <see cref="VelocityTrackerProcessor"/> to measure the interactors current velocity for throwing on release.
        /// </summary>
        [Serialized, Cleared]
        [field: DocumentedByXml, Restricted]
        public VelocityTrackerProcessor VelocityTracker { get; protected set; }
        /// <summary>
        /// The <see cref="ActiveCollisionPublisher"/> for checking valid start grabbing action.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public ActiveCollisionPublisher StartGrabbingPublisher { get; protected set; }
        /// <summary>
        /// The <see cref="ActiveCollisionPublisher"/> for checking valid stop grabbing action.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public ActiveCollisionPublisher StopGrabbingPublisher { get; protected set; }
        /// <summary>
        /// The processor for initiating an instant grab.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public GameObject InstantGrabProcessor { get; protected set; }
        /// <summary>
        /// The processor for initiating a precognitive grab.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public GameObject PrecognitionGrabProcessor { get; protected set; }
        /// <summary>
        /// The <see cref="CountdownTimer"/> to determine grab precognition.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public CountdownTimer PrecognitionTimer { get; protected set; }
        /// <summary>
        /// The minimum timer value for the grab precognition <see cref="CountdownTimer"/>.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public float MinPrecognitionTimer { get; protected set; } = 0.01f;
        /// <summary>
        /// The <see cref="GameObjectObservableSet"/> containing the currently grabbed objects.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public GameObjectObservableList GrabbedObjectsCollection { get; protected set; }
        /// <summary>
        /// A <see cref="BooleanAction"/> for holding the state of whether the Interactor is grabbing something.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml, Restricted]
        public BooleanAction IsGrabbingAction { get; protected set; }
        #endregion

        /// <summary>
        /// A collection of currently grabbed GameObjects.
        /// </summary>
        public IReadOnlyList<GameObject> GrabbedObjects => GrabbedObjectsCollection.NonSubscribableElements;

        /// <summary>
        /// A reusable instance of event data.
        /// </summary>
        protected readonly ActiveCollisionsContainer.EventData activeCollisionsEventData = new ActiveCollisionsContainer.EventData();

        /// <summary>
        /// Configures the action used to control grabbing.
        /// </summary>
        public virtual void ConfigureGrabAction()
        {
            if (GrabAction != null && Facade != null && Facade.GrabAction != null)
            {
                GrabAction.RunWhenActiveAndEnabled(() => GrabAction.ClearSources());
                GrabAction.RunWhenActiveAndEnabled(() => GrabAction.AddSource(Facade.GrabAction));
            }
        }

        /// <summary>
        /// Configures the velocity tracker used for grabbing.
        /// </summary>
        public virtual void ConfigureVelocityTrackers()
        {
            if (VelocityTracker != null && Facade != null && Facade.VelocityTracker != null)
            {
                VelocityTracker.VelocityTrackers.RunWhenActiveAndEnabled(() => VelocityTracker.VelocityTrackers.Clear());
                VelocityTracker.VelocityTrackers.RunWhenActiveAndEnabled(() => VelocityTracker.VelocityTrackers.Add(Facade.VelocityTracker));
            }
        }

        /// <summary>
        /// Configures the <see cref="CountdownTimer"/> components for grab precognition.
        /// </summary>
        public virtual void ConfigureGrabPrecognition()
        {
            if (Facade.GrabPrecognition < MinPrecognitionTimer && !Facade.GrabPrecognition.ApproxEquals(0f))
            {
                Facade.GrabPrecognition = MinPrecognitionTimer;
            }

            PrecognitionTimer.StartTime = Facade.GrabPrecognition;
            ChooseGrabProcessor();
        }

        /// <summary>
        /// Attempt to grab an Interactable to the current Interactor utilizing custom collision data and ungrabs any existing grab.
        /// </summary>
        /// <param name="interactable">The Interactable to attempt to grab.</param>
        /// <param name="collision">Custom collision data.</param>
        /// <param name="collider">Custom collider data.</param>
        public virtual void Grab(InteractableFacade interactable, Collision collision, Collider collider)
        {
            Grab(interactable, collision, collider, true);
        }

        /// <summary>
        /// Attempt to grab an Interactable to the current Interactor utilizing custom collision data and does not ungrab any existing grab..
        /// </summary>
        /// <param name="interactable">The Interactable to attempt to grab.</param>
        /// <param name="collision">Custom collision data.</param>
        /// <param name="collider">Custom collider data.</param>
        public virtual void GrabIgnoreUngrab(InteractableFacade interactable, Collision collision, Collider collider)
        {
            Grab(interactable, collision, collider, false);
        }

        /// <summary>
        /// Attempt to grab an Interactable to the current Interactor utilizing custom collision data.
        /// </summary>
        /// <param name="interactable">The Interactable to attempt to grab.</param>
        /// <param name="collision">Custom collision data.</param>
        /// <param name="collider">Custom collider data.</param>
        /// <param name="ungrabExistingGrab">Whether to ungrab any existing grab.</param>
        public virtual void Grab(InteractableFacade interactable, Collision collision, Collider collider, bool ungrabExistingGrab)
        {
            if (interactable == null)
            {
                return;
            }

            if (ungrabExistingGrab)
            {
                Ungrab();
            }

            StartGrabbingPublisher.SetActiveCollisions(CreateActiveCollisionsEventData(interactable.gameObject, collision, collider));
            ProcessGrabAction(StartGrabbingPublisher, true);
            if (interactable.IsGrabTypeToggle)
            {
                ProcessGrabAction(StartGrabbingPublisher, false);
            }
        }

        /// <summary>
        /// Attempt to ungrab currently grabbed Interactables to the current Interactor.
        /// </summary>
        public virtual void Ungrab()
        {
            if (GrabbedObjects.Count == 0)
            {
                return;
            }

            InteractableFacade interactable = GrabbedObjects[0].TryGetComponent<InteractableFacade>(true, true);
            if (interactable.IsGrabTypeToggle)
            {
                if (StartGrabbingPublisher.Payload.ActiveCollisions.Count == 0)
                {
                    StartGrabbingPublisher.SetActiveCollisions(CreateActiveCollisionsEventData(interactable.gameObject, null, null));
                }
                ProcessGrabAction(StartGrabbingPublisher, true);
            }
            ProcessGrabAction(StopGrabbingPublisher, false);
        }

        /// <summary>
        /// Attempts to automatically emit precognition grab if there are registered consumers.
        /// </summary>
        public virtual void PrecognitionGrabForRegisteredConsumers()
        {
            if (StartGrabbingPublisher.RegisteredConsumerContainer != null &&
                StartGrabbingPublisher.RegisteredConsumerContainer.RegisteredConsumers.Count > 0)
            {
                PrecognitionTimer.EmitStatus();
            }
        }

        protected virtual void OnEnable()
        {
            ConfigureGrabAction();
            ConfigureVelocityTrackers();
            ConfigureGrabPrecognition();
        }

        /// <summary>
        /// Chooses which grab processing to perform on the grab action.
        /// </summary>
        protected virtual void ChooseGrabProcessor()
        {
            bool disablePrecognition = PrecognitionTimer.StartTime.ApproxEquals(0f);
            InstantGrabProcessor.SetActive(disablePrecognition);
            PrecognitionGrabProcessor.SetActive(!disablePrecognition);
        }

        /// <summary>
        /// Processes the given collision data into a grab action based on the given state.
        /// </summary>
        /// <param name="publisher">The collision data to process.</param>
        /// <param name="actionState">The grab state to check against.</param>
        protected virtual void ProcessGrabAction(ActiveCollisionPublisher publisher, bool actionState)
        {
            InstantGrabProcessor.SetActive(false);
            PrecognitionGrabProcessor.SetActive(false);
            if (GrabAction.Value != actionState)
            {
                GrabAction.Receive(actionState);
            }
            if (GrabAction.Value)
            {
                publisher.Publish();
            }
            ChooseGrabProcessor();
        }

        /// <summary>
        /// Creates Active Collision data based on the given parameters.
        /// </summary>
        /// <param name="forwardSource">The source of the <see cref="GameObject"/> forwarding the collision data.</param>
        /// <param name="collision">The data on the point of the collision for precision grabbing.</param> 
        /// <param name="collider">The collider that has been collided with.</param>
        /// <returns></returns>
        protected virtual ActiveCollisionsContainer.EventData CreateActiveCollisionsEventData(GameObject forwardSource, Collision collision = null, Collider collider = null)
        {
            collider = collider == null ? forwardSource.GetComponentInChildren<Collider>() : collider;
            if (activeCollisionsEventData.ActiveCollisions.Count == 0)
            {
                activeCollisionsEventData.ActiveCollisions.Add(new CollisionNotifier.EventData());
            }
            activeCollisionsEventData.ActiveCollisions[0].Set(forwardSource.TryGetComponent<Component>(), collider.isTrigger, collision, collider);
            return activeCollisionsEventData;
        }

        /// <summary>
        /// Called after <see cref="VelocityTracker"/> has been changed.
        /// </summary>
        [CalledAfterChangeOf(nameof(VelocityTracker))]
        protected virtual void OnAfterVelocityTrackerChange()
        {
            ConfigureVelocityTrackers();
        }
    }
}
