// FreeFlyCamera.cs
using UnityEngine;

/// <summary>
/// 简单自由飞行摄像机（编辑器/运行时均可）
/// 鼠标右键按住可自由看向（或去掉条件直接鼠标控制）
/// WASD 移动，E/Q 上下，Shift 加速，鼠标滚轮调整基础速度
/// </summary>
[RequireComponent(typeof(Camera))]
public class FreeFlyCamera : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float boostMultiplier = 4f;
    public float mouseSensitivity = 4f;
    public bool holdRightMouseToLook = true;
    public bool invertY = false;
    public float scrollSpeedAdjust = 1f;

    private float yaw = 0f, pitch = 0f;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // look
        bool doLook = !holdRightMouseToLook || Input.GetMouseButton(1);
        if (doLook)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            yaw += mx * mouseSensitivity;
            pitch += (invertY ? my : -my) * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // movement
        Vector3 input = Vector3.zero;
        input += transform.forward * Input.GetAxis("Vertical");
        input += transform.right * Input.GetAxis("Horizontal");

        if (Input.GetKey(KeyCode.E)) input += transform.up;
        if (Input.GetKey(KeyCode.Q)) input -= transform.up;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= boostMultiplier;

        transform.position += input * speed * Time.deltaTime;

        // adjust base speed with mouse scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 1e-6f)
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed + scroll * scrollSpeedAdjust);
        }

        // lock/unlock cursor with Esc
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
