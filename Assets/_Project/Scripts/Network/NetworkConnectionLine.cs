using UnityEngine;
using Fusion;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkConnectionLine : NetworkBehaviour
    {
        [Networked] public NetworkId StartNodeID { get; set; }
        [Networked] public NetworkId EndNodeID { get; set; }

        private LineRenderer _lr;
        private Transform _startTrans;
        private Transform _endTrans;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.positionCount = 2;
        }

        public override void Spawned()
        {
            // Intentar buscar referencias iniciales
            FindTargets();
        }

        public void SetConnections(NetworkId start, NetworkId end)
        {
            if (Object.HasStateAuthority)
            {
                StartNodeID = start;
                EndNodeID = end;
            }
        }

        public override void Render()
        {
            if (_startTrans == null || _endTrans == null) FindTargets();

            if (_startTrans != null && _endTrans != null)
            {
                _lr.SetPosition(0, _startTrans.position);
                _lr.SetPosition(1, _endTrans.position);

                // --- ACTUALIZACIÓN DEL CUBO DE BORRADO ---
                UpdateHandlePosition();
            }
        }

        void FindTargets()
        {
            // Buscamos los objetos en la red usando sus IDs
            if (_startTrans == null && StartNodeID.IsValid)
            {
                if (Runner.TryFindObject(StartNodeID, out NetworkObject startObj))
                    _startTrans = startObj.transform;
            }

            if (_endTrans == null && EndNodeID.IsValid)
            {
                if (Runner.TryFindObject(EndNodeID, out NetworkObject endObj))
                    _endTrans = endObj.transform;
            }
        }

        // Variable para guardar el cubo
        private GameObject _deleteHandle;

        void UpdateHandlePosition()
        {
            // Si no existe el cubo, lo creamos
            if (_deleteHandle == null)
            {
                _deleteHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _deleteHandle.name = "DeleteHandle";
                _deleteHandle.transform.SetParent(transform);
                _deleteHandle.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

                var r = _deleteHandle.GetComponent<Renderer>();
                if (r) r.material.color = Color.red;

                var col = _deleteHandle.GetComponent<Collider>();
                if (col) col.isTrigger = true;
            }

            // Lo movemos al centro
            _deleteHandle.transform.position = (_startTrans.position + _endTrans.position) / 2;
        }
    }
}