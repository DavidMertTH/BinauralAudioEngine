using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.EditorControlls
{
    public class WallEditor : MonoBehaviour
    {
        public Material highlightMaterial;
        public Material wallMaterial;

        public bool isActive;
        public List<Wall> walls;

        private Vector3 _wallPositionA;
        private Vector3 _wallPositionB;
        private Wall _demoWall;

        private void Start()
        {
            isActive = true;
            _demoWall = Instantiate(new GameObject("demoWall")).AddComponent<Wall>();
            _demoWall.Init();
            _demoWall.SetMaterial(highlightMaterial);
            _demoWall.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!isActive) return;
            HandleInput();
        }

        private void HandleInput()
        {
            Vector3 clickedPosition = GetClickedPosition();
            if (_wallPositionA != Vector3.zero)
            {
                UpdateDemoWall(clickedPosition);
            }

            if (Input.GetMouseButtonDown(1))
            {
                ResetSelection();
            }

            if (Input.GetMouseButtonDown(0))
            {
                AddNewWallPosition(clickedPosition);
            }
        }

        private Vector3 GetClickedPosition()
        {
            Vector3 clickedPosition = Helper.GetMouseWorldPosition(Camera.main);
            clickedPosition.y = 0;
            if (Input.GetKey(KeyCode.LeftControl)) clickedPosition = SnapToGrid(clickedPosition);

            return clickedPosition;
        }

        private void UpdateDemoWall(Vector3 clickedPosition)
        {
            _demoWall.start = _wallPositionA;
            _demoWall.end = clickedPosition;
            _demoWall.CreateMesh();
        }

        private void AddNewWallPosition(Vector3 clickedPosition)
        {
            if (_wallPositionA == Vector3.zero)
            {
                _wallPositionA = clickedPosition;
                _demoWall.gameObject.SetActive(true);
                _demoWall.start = _wallPositionA;
                _demoWall.end = clickedPosition;
            }
            else
            {
                _wallPositionB = clickedPosition;
                walls.Add(CreateWall(_wallPositionA, _wallPositionB));
                ResetSelection();
                _demoWall.gameObject.SetActive(true);
                _wallPositionA = clickedPosition;
            }
        }

        private Vector3 SnapToGrid(Vector3 position)
        {
            float gridSize = 0.5f;

            position.x = Mathf.Round(position.x / gridSize) * gridSize;
            position.y = Mathf.Round(position.y / gridSize) * gridSize;
            position.z = Mathf.Round(position.z / gridSize) * gridSize;

            return position;
        }

        private void ResetSelection()
        {
            _wallPositionA = Vector3.zero;
            _wallPositionB = Vector3.zero;
            _demoWall.gameObject.SetActive(false);
        }

        private Wall CreateWall(Vector3 posA, Vector3 posB)
        {
            GameObject wallObject = new GameObject();
            Wall wall = wallObject.AddComponent<Wall>();
            wall.start = posA;
            wall.end = posB;
            wall.CreateMesh();
            wall.SetMaterial(wallMaterial);
            return wall;
        }

        private void OnDrawGizmos()
        {
            if (_wallPositionA != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_wallPositionA, 0.1f);
            }

            if (_wallPositionB != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_wallPositionB, 0.1f);
            }
        }
    }
}