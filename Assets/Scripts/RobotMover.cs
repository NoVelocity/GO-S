using UnityEngine;
using UnityEngine.AI;

public class RobotMover : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Настройки робота")]
    [Tooltip("Максимальный угол (в градусах), при котором роботу разрешено ехать вперед. Если угол больше — он крутится на месте.")]
    public float allowedAngleToMove = 15f; 

    public string robotId = "no id";
    [Header("Команды для отправки на робота IRL")]
    public float motorLeftSpeed;   // Скорость левого мотора (или общая скорость)
    public float motorRightSpeed;  // Скорость правого мотора (или угловая скорость)
    public string robotCommand;    // Для наглядности: "ROTATE", "MOVE", "STOP"

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    // Вызывать при получении координат от реального робота
    public void UpdateRobotPositionFromIRL(Vector3 realRobotPosition, Quaternion realRobotRotation)
    {
        transform.position = realRobotPosition;
        transform.rotation = realRobotRotation;
        agent.nextPosition = realRobotPosition; // Привязываем агента
    }

    public void SetNavMeshDestination(Vector3 targetDest)
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        agent.SetDestination(targetDest);
    }

    void Update()
    {
        // 1. Проверяем, есть ли вообще путь и нужно ли двигаться
        if (agent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            // Направление, куда ТРЕБУЕТСЯ ехать по мнению NavMesh
            Vector3 targetDirection = agent.desiredVelocity.normalized;
            targetDirection.y = 0; // Игнорируем высоту

            // Текущее направление, куда СМОТРИТ робот в Unity (и в реальности)
            Vector3 currentForward = transform.forward;
            currentForward.y = 0;

            // 2. Считаем угол (в градусах) между "куда смотрю" и "куда надо ехать"
            float angleError = Vector3.Angle(currentForward, targetDirection);

            // Определяем знак угла (в какую сторону крутиться: влево или вправо)
            float turnSign = Mathf.Sign(Vector3.SignedAngle(currentForward, targetDirection, Vector3.up));

            // 3. ЛОГИКА РАЗДЕЛЕНИЯ ДВИЖЕНИЯ
            if (angleError > allowedAngleToMove)
            {
                // Угол слишком большой! Робот должен СТОЯТЬ на месте и только КРУТИТЬСЯ
                robotCommand = "ROTATE";
                
                // Пример для дифференциального привода (одно колесо вперед, другое назад)
                motorLeftSpeed = -1f * turnSign; 
                motorRightSpeed = 1f * turnSign;
            }
            else
            {
                // Мы почти довернулись на цель! Можно ЕХАТЬ ВПЕРЕД
                robotCommand = "MOVE_FORWARD";

                // Едем прямо, но делаем микро-подруливания, если угол не идеально нулевой
                float steeringCorrection = (angleError / allowedAngleToMove) * turnSign * 0.3f;
                motorLeftSpeed = 1f - steeringCorrection;
                motorRightSpeed = 1f + steeringCorrection;
            }

            // Визуализация в Unity
            Debug.DrawRay(transform.position, targetDirection * 2f, Color.green); // Куда надо
            Debug.DrawRay(transform.position, transform.forward * 2f, Color.blue); // Куда смотрит сейчас
        }
        else
        {
            // Приехали или стоим
            robotCommand = "STOP";
            motorLeftSpeed = 0f;
            motorRightSpeed = 0f;
        }
    }
}