using System.Collections.Generic;
using GoLive.Items;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoLive.PcBuilding
{
    // A see-through copy of the held part's meshes shown on a slot's install anchor before the player confirms, so
    // the preview pose is the installed pose by construction. Presentation only: no WorldItem, Rigidbody or
    // Collider, never saved and never registered as an item. Rebuilt only when a different kind of part is shown.
    internal sealed class PcInstallGhost
    {
        private readonly Material _material;
        private readonly List<MeshFilter> _sourceFilters = new();

        private GameObject _root;
        private ItemDefinition _builtFor;

        public bool IsVisible => _root != null && _root.activeSelf;
        public GameObject Root => _root;

        public PcInstallGhost(Material material)
        {
            _material = material;
        }

        public void Show(WorldItem part, Transform installAnchor)
        {
            if (_root == null || _builtFor != part.Definition)
                Rebuild(part);

            _root.transform.SetParent(installAnchor, false);
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);

            _root = null;
            _builtFor = null;
        }

        private void Rebuild(WorldItem part)
        {
            Dispose();

            _root = new GameObject($"Install ghost ({part.Definition.ItemId})");
            _root.SetActive(false);

            Matrix4x4 partToLocal = part.transform.worldToLocalMatrix;
            part.GetComponentsInChildren(true, _sourceFilters);

            for (int i = 0; i < _sourceFilters.Count; i++)
            {
                MeshFilter source = _sourceFilters[i];

                if (source.sharedMesh == null || !source.TryGetComponent(out MeshRenderer _))
                    continue;

                Matrix4x4 relative = partToLocal * source.transform.localToWorldMatrix;

                GameObject piece = new(source.name);
                piece.transform.SetParent(_root.transform, false);
                piece.transform.SetLocalPositionAndRotation(relative.GetColumn(3), relative.rotation);
                piece.transform.localScale = relative.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;

                MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
                Material[] materials = new Material[source.sharedMesh.subMeshCount];

                for (int m = 0; m < materials.Length; m++)
                    materials[m] = _material;

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _sourceFilters.Clear();
            _builtFor = part.Definition;
        }
    }
}
