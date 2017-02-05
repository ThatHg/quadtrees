using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;


namespace Quadtree {
    public class Quadtree : MonoBehaviour {
        [SerializeField] private Transform _circle;
        [SerializeField] private Transform _negative_circle;
        [SerializeField] private Transform _area;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private float _move_speed = 0.1f;
        [SerializeField] private Text _active_nodes;
        [SerializeField] private Text _tree_calculation_time;
        [SerializeField] private Text _depth_text;
        [SerializeField] private Text _square_size_text;
        [SerializeField] private Text _circle_size_text;
        [SerializeField] private Text _negative_circle_size_text;

        // Consts
        private const float MAX_SIZE = 16;
        private const int MAX_NODES = 4096;

        // Buffer
        private List<GameObject> _used_prefabs = new List<GameObject>();

        // Nodes
        private float _square_size = 8;
        private int _depth = 3;
        private int _node_count = 0;
        private Node[] _drawable_nodes = new Node[MAX_NODES];

        // Mouse
        private Vector3 _mouse_position;

        // Debug
        private float _timer;
        private bool _is_drawing = false;
        private Stopwatch _stopwatch = new Stopwatch();

        // Root node
        private Node _root_node;

        public void ChangeDepth(float value) {
            _depth = (int)value;
            _depth_text.text = _depth.ToString();
        }

        public void ChangeSquareSize(float value) {
            value = Mathf.Clamp(value, 0f, 1f);

            _square_size = value * MAX_SIZE;
            var scale = _area.transform.localScale;
            scale.x = _square_size;
            scale.y = _square_size;
            _area.transform.localScale = scale;
            _square_size_text.text = _square_size.ToString();
        }

        public void ChangeCircleSize(float value) {
            value = Mathf.Clamp(value, 0f, 1f);

            var size = value * MAX_SIZE;
            var scale = _circle.transform.localScale;
            scale.x = size;
            scale.y = size;
            _circle.transform.localScale = scale;
            _circle_size_text.text = size.ToString();
        }

        public void ChangeNegativeCircleSize(float value) {
            value = Mathf.Clamp(value, 0f, 1f);

            var size = value * MAX_SIZE;
            var scale = _negative_circle.transform.localScale;
            scale.x = size;
            scale.y = size;
            _negative_circle.transform.localScale = scale;
            _negative_circle_size_text.text = size.ToString();
        }

        private void Start() {
            var size = Mathf.Max(_area.transform.localScale.x, _area.transform.localScale.y);
            _root_node = new Node();
            _root_node.Position = _area.transform.position + new Vector3(-size * 0.5f, size * 0.5f, 0);
            _root_node.Size = size;

            ChangeCircleSize(0.5f);
            ChangeSquareSize(0.5f);
            ChangeDepth(5);
        }

        private void Update() {
            var overUI = EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButton(1) && overUI == false) { // Move square area
                _mouse_position = Input.mousePosition;
                _mouse_position = Camera.main.ScreenToWorldPoint(_mouse_position);
                _area.transform.position = Vector2.Lerp(_area.transform.position, _mouse_position, _move_speed);
            }
            else if (Input.GetMouseButton(0) && overUI == false) { // Move circle
                _mouse_position = Input.mousePosition;
                _mouse_position = Camera.main.ScreenToWorldPoint(_mouse_position);
                _circle.transform.position = Vector2.Lerp(_circle.transform.position, _mouse_position, _move_speed);
            }
            else if (Input.GetMouseButton(2) && overUI == false) { // Move negative circle
                _mouse_position = Input.mousePosition;
                _mouse_position = Camera.main.ScreenToWorldPoint(_mouse_position);
                _negative_circle.transform.position = Vector2.Lerp(_negative_circle.transform.position, _mouse_position, _move_speed);
            }

            if (Time.realtimeSinceStartup - _timer > 0.01 && _is_drawing == false) {
                _timer = Time.realtimeSinceStartup;

                // Update the root node.
                _root_node.Position = _area.transform.position + new Vector3(-_square_size * 0.5f, _square_size * 0.5f, 0);
                _root_node.Size = _square_size;

                // Monitor calculation time with stopwatch
                _stopwatch.Reset();
                _stopwatch.Start();
                CalculateTree(_root_node);
                _stopwatch.Stop();

                // Update gui.
                _active_nodes.text = _node_count.ToString();
                _tree_calculation_time.text = _stopwatch.ElapsedMilliseconds.ToString() + "ms";

                // Draw quadtree
                ShowTree();
            }
        }

        private void CalculateTree(Node root) {
            var c_pos = _circle.position;
            var c_radius = _circle.localScale.x * 0.5f;
            var neg_c_radius = _negative_circle.localScale.x * 0.5f;
            var neg_c_pos = _negative_circle.transform.position;
            var maximum_depth_size = root.Size * (Mathf.Pow(0.5f, _depth + 1));

            Queue<Node> nodes = new Queue<Node>();
            _node_count = 0;

            // Enqueue our first node to check.
            nodes.Enqueue(root);
            while (nodes.Count > 0) {
                // Get the first node in queue
                var node = nodes.Dequeue();

                // Check if node is inside Negative circle area, this is the same as outside main circle area.
                // Split node in quad tree if it intersects with edge of negative or main circle area.
                // Always check if inside main area before check if intersecting with edge of main area.
                // 
                if(InsideCircle(node, neg_c_pos, neg_c_radius)) {
                    node.State = CollisionState.Outside;
                }
                else if (IntersectCircle(node, neg_c_pos, neg_c_radius)) { 
                    node.State = CollisionState.Unsure;
                }
                else if (InsideCircle(node, c_pos, c_radius)) { 
                    node.State = CollisionState.Inside;
                }
                else if (IntersectCircle(node, c_pos, c_radius)) {
                    node.State = CollisionState.Unsure;
                }
                else {
                    node.State = CollisionState.Outside;
                }

                // Add node to drawable nodes if fully inside main area
                if(node.State == CollisionState.Inside) {
                    if(MAX_NODES > _node_count) {
                        _drawable_nodes[_node_count].Copy(node);
                        _node_count++;
                    }
                    else {
                        break;
                    }
                }
                else if (node.State == CollisionState.Unsure) {
                    // Split node in quadtree if unsure.
                    if (node.Size >= maximum_depth_size) {
                        node.Split();
                        var childs = node.Childs;
                        for (int i = 0; i < childs.Length; ++i) {
                            nodes.Enqueue(childs[i]);
                        }
                    }
                }
            }
        }

        private void ShowTree() {
            _is_drawing = true;

            // Deactivate all prefabs past _node_count, these will not be used.
            for(int i = _node_count; i < _used_prefabs.Count; ++i) {
                _used_prefabs[i].SetActive(false);
            }

            // Move, set scale and activate prefabs to show
            // the quadtree.
            for (int i = 0; i < _node_count; ++i) {
                Node node = _drawable_nodes[i];
                float half_size = node.Size * 0.5f;
                GameObject go = null;
                // We should only add more GameObjects if we need to.
                if (i > _used_prefabs.Count - 1) {
                    go = (GameObject)Instantiate(_prefab, node.Position + new Vector3(half_size, -half_size, 0), Quaternion.identity);
                    _used_prefabs.Add(go);
                }
                else { // Otherwise use old instantiated prefabs.
                    go = _used_prefabs[i];
                    go.transform.position = node.Position + new Vector3(half_size, -half_size, 0);
                    go.SetActive(true);
                }

                // Add spacing to nodes so we get lines between them,
                // This will make the quad tree visible.
                var scale = node.Size - 0.06f;
                if (node.Size <= 0.3f) { // scale with multiples when it scale gets to small.
                    scale = node.Size * 0.92f;
                }
                go.transform.localScale = new Vector3(scale, scale, 1);
            }
            _is_drawing = false;
        }

        /// <summary>
        /// Check if node square is completly inside circle.
        /// This checks all corner vertices on axis aligned node square,
        /// if all are inside circle, then square must be completly inside.
        /// </summary>
        /// <param name="node">Node to check agains circle</param>
        /// <returns>true if totaly inside. false otherwise</returns>
        private bool InsideCircle(Node node, Vector3 c_pos, float c_radius) {
            var dist0 = (node.Position - c_pos).sqrMagnitude;
            var dist1 = (node.Position + new Vector3(node.Size, 0f, 0f) - c_pos).sqrMagnitude;
            var dist2 = (node.Position + new Vector3(0f, -node.Size, 0f) - c_pos).sqrMagnitude;
            var dist3 = (node.Position + new Vector3(node.Size, -node.Size, 0f) - c_pos).sqrMagnitude;
            var square_radius = c_radius * c_radius;
            return dist0 < square_radius &&
                dist1 < square_radius &&
                dist2 < square_radius &&
                dist3 < square_radius;
        }

        /// <summary>
        /// Check if any edges on node square intersects with circle
        /// </summary>
        /// <param name="node">Node to check with</param>
        /// <returns>true if any edge intersects with circle, false otherwise</returns>
        private bool IntersectCircle(Node node, Vector3 c_pos, float c_radius) {
            var p = node.Position;
            var x1 = p.x;               // Left
            var y1 = p.y;               // Top
            var x2 = p.x + node.Size;   // Right
            var y2 = p.y - node.Size;   // Bottom

            var delta_x = c_pos.x - Mathf.Max(x1, Mathf.Min(c_pos.x, x2));
            var delta_y = c_pos.y - Mathf.Max(y2, Mathf.Min(c_pos.y, y1));
            return (delta_x * delta_x + delta_y * delta_y) < (c_radius * c_radius);
        }
    }

    public enum CollisionState {
        Unsure = 0,     // Unsure means node is not fully outside or inside an area.
        Inside = 1,     // Completly inside an area.
        Outside = 2,    // Completly outside an area.
    }

    public struct Node {
        public Vector3 Position { set; get; }
        public float Size { set; get; }
        public Node[] Childs { private set; get; }
        public CollisionState State { set; get; }

        public void Copy(Node node) {
            Position = node.Position;
            Size = node.Size;
            State = node.State;

            if(node.Childs != null) {
                if (Childs == null) {
                    Childs = new Node[4];

                    Childs[0].Copy(node);
                    Childs[1].Copy(node);
                    Childs[2].Copy(node);
                    Childs[3].Copy(node);
                }
            }
        }

        public void Split() {
            float half_size = Size * 0.5f;
            if (Childs == null) {
                Childs = new Node[4] { new Node(), new Node(), new Node(), new Node() };
            }

            Childs[0].Position = Position;
            Childs[0].Size = half_size;
            Childs[0].State = CollisionState.Outside;

            Childs[1].Position = Position + new Vector3(half_size, 0, 0);
            Childs[1].Size = half_size;
            Childs[1].State = CollisionState.Outside;

            Childs[2].Position = Position + new Vector3(0, -half_size, 0);
            Childs[2].Size = half_size;
            Childs[2].State = CollisionState.Outside;

            Childs[3].Position = Position + new Vector3(half_size, -half_size, 0);
            Childs[3].Size = half_size;
            Childs[3].State = CollisionState.Outside;
        }
    }
}
