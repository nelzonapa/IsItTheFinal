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
            // Si aún no tenemos los transform, intentamos buscarlos (pueden tardar en spawnear)
            if (_startTrans == null || _endTrans == null)
            {
                FindTargets();
            }

            // Si ya los tenemos, actualizamos la línea visual
            if (_startTrans != null && _endTrans != null)
            {
                _lr.SetPosition(0, _startTrans.position);
                _lr.SetPosition(1, _endTrans.position);
            }
            else
            {
                // Si falta un nodo (ej. fue borrado), la línea debería desaparecer
                // En un entorno ideal, el dueño debería llamar a Runner.Despawn(Object)
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
    }
}