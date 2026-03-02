using UnityEngine;

namespace ImmersiveGraph.Visual
{
    /// <summary>
    /// Clase estática encargada puramente de la geometría del grafo.
    /// No depende de GameObjects ni de Unity Context, solo matemáticas.
    /// </summary>
    public static class HyperbolicMath
    {
        /// <summary>
        /// Calcula posiciones distribuidas uniformemente en un HEMISFERIO (Media esfera).
        /// Útil para los nodos hijos (Archivos) para que no toquen la línea del padre.
        /// </summary>
        /// <param name="count">Cantidad de nodos a colocar.</param>
        /// <param name="radius">Radio de la órbita.</param>
        /// <param name="direction">Dirección hacia donde debe apuntar la copa del hemisferio (Vector desde el Abuelo al Padre).</param>
        /// <returns>Array de posiciones en espacio local.</returns>
        public static Vector3[] GetOrientedHemisphere(int count, float radius, Vector3 direction)
        {
            if (count <= 0) return new Vector3[0];
            if (count == 1) return new Vector3[] { direction.normalized * radius };

            Vector3[] points = new Vector3[count];

            // Constante del Ángulo Dorado para distribución natural (Fibonacci)
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

            // 1. Calculamos la rotación necesaria para mirar hacia la "direction" deseada
            // Por defecto, la fórmula matemática genera el hemisferio apuntando hacia Arriba (Vector3.up)
            Quaternion lookRot = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            for (int i = 0; i < count; i++)
            {
                // --- FÓRMULA DE FIBONACCI MODIFICADA PARA HEMISFERIO ---

                // En una esfera normal, 'y' va de 1 a -1.
                // Para un hemisferio, queremos que 'y' vaya solo de 1 a 0 (o un poco más arriba para que no queden muy planos).
                // Ajustamos el rango: i / count va de 0 a 1.

                float k = i + 0.5f; // Pequeño ajuste para evitar bordes extremos

                // Mapeamos la altura (y) entre 1.0 (polo) y 0.2 (casi base). 
                // Evitamos el 0.0 absoluto para que no queden perfectamente planos a los lados.
                float y = 1f - (k / (float)count) * 0.8f;

                // Radio del círculo a esta altura 'y'
                float radiusAtY = Mathf.Sqrt(1f - y * y);

                // Ángulo alrededor del eje
                float theta = phi * i;

                // Coordenadas en espacio canónico (apuntando hacia arriba)
                float x = Mathf.Cos(theta) * radiusAtY;
                float z = Mathf.Sin(theta) * radiusAtY;

                // Vector base
                Vector3 basePoint = new Vector3(x, y, z) * radius;

                // 2. Aplicamos la rotación para que apunte hacia afuera del centro del grafo
                points[i] = lookRot * basePoint;
            }

            return points;
        }

        /// <summary>
        /// La versión clásica de esfera completa (para el Nivel 1: Comunidades alrededor de la Raíz).
        /// </summary>
        public static Vector3[] GetFibonacciSphere(int count, float radius)
        {
            if (count <= 0) return new Vector3[0];
            Vector3[] points = new Vector3[count];
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < count; i++)
            {
                // 'y' va de 1 a -1 (Esfera completa)
                float y = 1 - (i / (float)(count - 1)) * 2;
                float radiusAtY = Mathf.Sqrt(1 - y * y);
                float theta = phi * i;

                float x = Mathf.Cos(theta) * radiusAtY;
                float z = Mathf.Sin(theta) * radiusAtY;

                points[i] = new Vector3(x * radius, y * radius, z * radius);
            }
            return points;
        }

        /// <summary>
        /// Calcula posiciones en una BANDA CURVA (Varias filas).
        /// Distribuye los nodos en una grilla proyectada sobre un cilindro.
        /// </summary>
        /// <param name="count">Cantidad total de nodos.</param>
        /// <param name="radius">Distancia al usuario.</param>
        /// <param name="arcAngle">Ancho del arco (grados).</param>
        /// <param name="rows">Cuántas filas de altura queremos.</param>
        /// <returns></returns>
        public static Vector3[] GetHorizonArcPositions(int count, float radius, float arcAngle = 160f, int rows = 5)
        {
            if (count <= 0) return new Vector3[0];
            if (count == 1) return new Vector3[] { Vector3.zero };

            Vector3[] points = new Vector3[count];

            // 1. Calcular columnas necesarias según la cantidad de filas deseadas
            int columns = Mathf.CeilToInt((float)count / rows);

            // 2. Definir el paso angular (ancho de cada celda)
            float startAngle = -arcAngle / 2f;
            float angleStep = arcAngle / Mathf.Max(1, columns - 1);

            // 3. Definir altura de las filas
            float rowHeight = 2.0f; // Distancia vertical entre nodos (30cm)
            // Calculamos el inicio para que el bloque quede centrado verticalmente
            float startHeight = -((rows - 1) * rowHeight) / 2f;

            for (int i = 0; i < count; i++)
            {
                // Calcular en qué fila y columna cae este nodo
                int colIndex = i % columns; // Avanza en horizontal
                int rowIndex = i / columns; // Avanza en vertical cuando se llena la columna

                // --- CALCULO HORIZONTAL (ARCO) ---
                float currentAngle = startAngle + (angleStep * colIndex);
                float rad = currentAngle * Mathf.Deg2Rad;

                float x = Mathf.Sin(rad) * radius;
                float z = Mathf.Cos(rad) * radius * 0.5f; // Profundidad curva

                // --- CALCULO VERTICAL (ALTURA) ---
                // Invertimos (rows - 1 - rowIndex) si quieres que se llene de arriba a abajo, 
                // o lo dejamos así para de abajo a arriba.
                float y = startHeight + (rowIndex * rowHeight);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }
    }

}