using Code.EditorControlls;
using UnityEngine;

namespace Code.EditorControls
{
    public class ClickController : MonoBehaviour
    {
        private ObjectDragger _objectDragger;
        private WallEditor _wallEditor;

        private void Start()
        {
            _wallEditor = GetComponent<WallEditor>();
            _objectDragger = GetComponent<ObjectDragger>();
            if (_wallEditor == null) gameObject.AddComponent<WallEditor>();
            if (_objectDragger == null) gameObject.AddComponent<ObjectDragger>();
        }

        private void Update()
        {
            _objectDragger.ListenForInput();
            if (!_objectDragger.HitsDraggableObject()) _wallEditor.ListenForInputs();
        }
    }
}