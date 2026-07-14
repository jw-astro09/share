using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동 속도 설정")]
    public float moveSpeed = 5f; // 캐릭터의 이동 속도

    private Rigidbody2D rb;      // 물리 연산을 위한 컴포넌트
    private Vector2 moveInput;   // 키보드 입력 값을 저장할 변수

    void Start()
    {
        // 게임이 시작되면 이 스크립트가 붙은 오브젝트에서 Rigidbody2D를 찾아 연결합니다.
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // GetAxisRaw는 유니티에서 기본적으로 WASD와 방향키를 모두 감지합니다.
        // 누르지 않으면 0, 왼쪽/아래는 -1, 오른쪽/위는 1을 반환합니다.
        moveInput.x = Input.GetAxisRaw("Horizontal"); // 좌우 입력
        moveInput.y = Input.GetAxisRaw("Vertical");   // 상하 입력
    }

    void FixedUpdate()
    {
        // 대각선 이동 시 속도가 빨라지는 것을 막기 위해 normalized(정규화)를 해줍니다.
        // 물리 컴포넌트(rb)를 이용해 벽에 부딪혀도 뚫리지 않고 멈추도록 이동시킵니다.
        rb.MovePosition(rb.position + moveInput.normalized * moveSpeed * Time.fixedDeltaTime);
    }
}