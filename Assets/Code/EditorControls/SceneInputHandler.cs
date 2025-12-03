using System;
using Code.EditorControlls;
using UnityEngine;

namespace Code.EditorControls
{
    public class SceneInputHandler : MonoBehaviour
    {
        private WallEditor _wallEditor;

        // private ObjectInteraction _objectInteraction;
        public InteractionState state;

        public enum InteractionState
        {
            WallBuilder,
            ObjectInteraction,
        }

        void Start()
        {
            _wallEditor = GetComponent<WallEditor>();
            if (_wallEditor == null) _wallEditor = gameObject.AddComponent<WallEditor>();
            // _objectInteraction = new ObjectInteraction();
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                state = InteractionState.WallBuilder;
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                state = InteractionState.ObjectInteraction;
            }

            if (state == InteractionState.WallBuilder)
            {
                _wallEditor.ListenForInputs();
            }

            if (state == InteractionState.ObjectInteraction)
            {
                // _objectInteraction.HandleInput();
            }
        }
    }
}