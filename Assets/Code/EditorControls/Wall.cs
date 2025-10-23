using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.EditorControlls
{
    public class Wall : MonoBehaviour
    {
        public Vector3 start;
        public Vector3 end;
        private readonly float _wallThickness = 0.1f;
        private readonly float _wallHeight = 2.4f;

        private MeshCollider _collider;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private bool _isInitialized = false;

        public void Init()
        {
            if(_isInitialized)return;
            transform.localScale = Vector3.one;
            _isInitialized = true;
            _collider = gameObject.AddComponent<MeshCollider>();
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshFilter.mesh = new Mesh();
            _collider.sharedMesh =  _meshFilter.mesh;

        }

        public void SetMaterial(Material material)
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshRenderer.material = material;
        }

        public void CreateMesh()
        {
            Init();
            Vector3 wallDir = end - start;
            Vector3 orthogonalDir = Vector3.Cross(wallDir, Vector3.up).normalized;
            Vector3 height = Vector3.up * _wallHeight;

            Vector3[] vertices = new Vector3[8];
            int[] triangles = new int[36];

            vertices[0] = start + orthogonalDir * _wallThickness / 2;
            vertices[1] = start - orthogonalDir * _wallThickness / 2;
            vertices[2] = end + orthogonalDir * _wallThickness / 2;
            vertices[3] = end - orthogonalDir * _wallThickness / 2;

            vertices[4] = start + orthogonalDir * _wallThickness / 2 + height;
            vertices[5] = start - orthogonalDir * _wallThickness / 2 + height;
            vertices[6] = end + orthogonalDir * _wallThickness / 2 + height;
            vertices[7] = end - orthogonalDir * _wallThickness / 2 + height;

            //Bottom
            triangles[0] = 1;
            triangles[1] = 2;
            triangles[2] = 0;

            triangles[3] = 1;
            triangles[4] = 3;
            triangles[5] = 2;

            //Top
            triangles[6] = 4;
            triangles[7] = 6;
            triangles[8] = 5;

            triangles[9] = 6;
            triangles[10] = 7;
            triangles[11] = 5;

            //Start
            triangles[12] = 0;
            triangles[13] = 4;
            triangles[14] = 1;

            triangles[15] = 1;
            triangles[16] = 4;
            triangles[17] = 5;

            //End
            triangles[18] = 3;
            triangles[19] = 6;
            triangles[20] = 2;

            triangles[21] = 3;
            triangles[22] = 7;
            triangles[23] = 6;

            //left
            triangles[24] = 3;
            triangles[25] = 1;
            triangles[26] = 5;

            triangles[27] = 7;
            triangles[28] = 3;
            triangles[29] = 5;

            //right
            triangles[30] = 0;
            triangles[31] = 2;
            triangles[32] = 6;

            triangles[33] = 6;
            triangles[34] = 4;
            triangles[35] = 0;

            _meshFilter.mesh.vertices = vertices;
            _meshFilter.mesh.triangles = triangles;
            _meshFilter.mesh.RecalculateBounds();
            _meshFilter.mesh.RecalculateNormals();
            _collider.convex = true;
            _collider.sharedMesh.RecalculateBounds();
        }
    }
}