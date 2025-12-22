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

        // Variable para guardar el cubo
        private GameObject _deleteHandle;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.positionCount = 2;
        }

        public override void Spawned()
        {
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
            // 1. Buscar objetivos si faltan
            if (_startTrans == null || _endTrans == null) FindTargets();

            // 2. Si tenemos ambos, dibujamos
            if (_startTrans != null && _endTrans != null)
            {
                _lr.enabled = true;
                _lr.SetPosition(0, _startTrans.position);
                _lr.SetPosition(1, _endTrans.position);

                // Actualizar la posición del cubo
                UpdateHandlePosition();
            }
            else
            {
                // Si falta un nodo, ocultamos todo para evitar líneas al infinito
                _lr.enabled = false;
                if (_deleteHandle != null) _deleteHandle.SetActive(false);
            }
        }

        void FindTargets()
        {
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

        void UpdateHandlePosition()
        {
            // CREACIÓN DEL CUBO (Solo una vez)
            if (_deleteHandle == null)
            {
                _deleteHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _deleteHandle.name = "DeleteHandle";

                // Hacemos al cubo hijo de la línea para que se borre si la línea se borra
                _deleteHandle.transform.SetParent(transform);
                _deleteHandle.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

                // --- ARREGLO DEL COLOR ROSA ---
                var r = _deleteHandle.GetComponent<Renderer>();
                if (r != null)
                {
                    // Asignamos el shader "Sprites/Default" que siempre funciona en VR/Android
                    // y es muy ligero.
                    r.material = new Material(Shader.Find("Sprites/Default"));
                    r.material.color = Color.red;
                }
                // ------------------------------

                var col = _deleteHandle.GetComponent<Collider>();
                if (col) col.isTrigger = true;
            }

            // MANTENER POSICIÓN
            if (!_deleteHandle.activeSelf) _deleteHandle.SetActive(true);

            // Calculamos el punto medio exacto en el mundo
            Vector3 midPoint = (_startTrans.position + _endTrans.position) / 2;

            // Asignamos la posición
            _deleteHandle.transform.position = midPoint;

            // Opcional: Reiniciar la rotación para que el cubo siempre esté alineado con el mundo
            // y no gire raro si la línea tiene rotación.
            _deleteHandle.transform.rotation = Quaternion.identity;
        }

        // Limpieza: Si este objeto se destruye, Unity destruye el hijo (_deleteHandle) automáticamente.
    }
}