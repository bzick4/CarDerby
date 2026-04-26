# Cinemachine Network Camera System

Полная система управления камерой для сетевых игр на Netcode for GameObjects с интеграцией Cinemachine.

## 📦 Компоненты системы

### 1. **CinemachineNetworkCamera** (Базовая камера для Netcode)
Основной скрипт, который обеспечивает следование за транспортным средством локального игрока.

**Особенности:**
- Работает только для локального игрока (автоматически отключается для других)
- Гладкое следование благодаря Transposer
- Динамическая подстройка высоты камеры
- Легко интегрируется с любым NetworkBehaviour

**Как использовать:**
1. Создайте Virtual Camera в сцене
2. Добавьте компонент `CinemachineNetworkCamera` на неё
3. Настройте параметры (расстояние, высота, damping)
4. Автоматически активируется для локального игрока

```csharp
// Пример использования в коде
var cameraOffset = virtualCam.GetCameraOffset();
virtualCam.SetCameraOffset(newOffset);
```

### 2. **AdvancedNetworkCamera** (Продвинутая камера со скоростью)
Расширенная версия с поддержкой динамических эффектов.

**Особенности:**
- Расстояние камеры меняется в зависимости от скорости
- Look-ahead (предварительный просмотр в направлении движения)
- Optional наклон камеры при ускорении
- Гладкие переходы эффектов

**Параметры:**
- `baseFollowDistance`: базовое расстояние (когда машина стоит)
- `maxSpeedFollowDistance`: расстояние при максимальной скорости
- `lookAheadDistance`: расстояние опережающего просмотра
- `enableSpeedTilt`: наклон камеры при движении
- `maxTiltAngle`: максимальный угол наклона

### 3. **CameraManager** (Управление камер и эффекты)
Singleton для управления несколькими камерами и применения глобальных эффектов.

**Функции:**
- Регистрация и активация камер
- Camera shake эффекты
- Smooth переходы между камерами
- Применение предустановок
- Dynamic FOV control

**Использование:**
```csharp
// Эффект тряски камеры
CameraManager.Instance.ApplyCameraShake(1f, 0.2f);

// Смена камеры
CameraManager.Instance.ActivateCamera("chase_camera");

// Zoom эффект
CameraManager.Instance.CameraZoomEffect(75f, 1f);

// Изменение FOV
CameraManager.Instance.SetCameraFOV(65f);
```

### 4. **VehicleCameraController** (Интеграция с VehicleController)
Связывает камеру с вашим VehicleController для эффектов, основанных на движении машины.

**Особенности:**
- Dynamic FOV (увеличивается при ускорении)
- Collision shake (тряска при столкновении)
- Режим спектатора
- Специальные камера-эффекты

**Использование:**
```csharp
// При столкновении машины
vehicleCameraController.OnVehicleCollision(impactForce);

// Режим спектатора
vehicleCameraController.SetSpectatorMode(otherPlayerVehicle, distance: 8f, height: 3f);

// Вернуться в обычный режим
vehicleCameraController.SetFollowMode();

// Получить текущую скорость для UI
float speed = vehicleCameraController.GetCurrentSpeed();
```

## 🎮 Настройка в Unity

### Шаг 1: Иерархия сцены
```
PlayerVehicle (NetworkObject)
├── VehicleModel
├── Wheels
├── Scripts
│   ├── VehicleController
│   └── VehicleCameraController
└── VirtualCamera (CinemachineVirtualCamera)
    ├── CinemachineNetworkCamera или AdvancedNetworkCamera
    ├── Transposer
    ├── Composer
    └── Noise (опционально для shake)

Canvas
└── CameraManager (Singleton)
```

### Шаг 2: Конфигурация Virtual Camera
1. Добавьте компонент `CinemachineVirtualCamera`
2. Установите Priority выше чем другие камеры
3. Добавьте Transposer в Body
4. Добавьте Composer в Aim
5. Добавьте Noise (если хотите shake эффекты)

### Шаг 3: Присоединение скриптов
1. На Virtual Camera добавьте `CinemachineNetworkCamera` или `AdvancedNetworkCamera`
2. На PlayerVehicle добавьте `VehicleCameraController`
3. Создайте пустой GameObject с `CameraManager`

### Шаг 4: Подключение ссылок
```inspector
VehicleCameraController:
├── networkCamera: [Virtual Camera с CinemachineNetworkCamera]
├── advancedCamera: [Virtual Camera с AdvancedNetworkCamera]
├── collisionShakeDuration: 0.2
└── collisionShakeIntensity: 0.5

CameraManager:
├── cinemachineBrain: [Camera в сцене]
└── cameraPresets: [Ваши предустановки]
```

## 🔧 Примеры интеграции с VehicleController

Добавьте в ваш VehicleController:

```csharp
[SerializeField] private VehicleCameraController _cameraController;

private void OnCollisionEnter(Collision collision)
{
    float impactForce = collision.relativeVelocity.magnitude;
    _cameraController?.OnVehicleCollision(impactForce);
}
```

## 📊 Параметры камеры для разных стилей

### Режим "Гонка"
```
Follow Distance: 5-6
Follow Height: 2.5-3
Damping: 0.08-0.12
FOV: 60-65
```

### Режим "Action"
```
Follow Distance: 4-5
Follow Height: 2-2.5
Damping: 0.05-0.08
FOV: 65-70
```

### Режим "Cinematic"
```
Follow Distance: 8-10
Follow Height: 3-4
Damping: 0.15-0.2
FOV: 55-60
MaxTilt: 0 (отключить наклон)
```

## 🐛 Решение проблем

**Камера не следует за машиной:**
- Убедитесь, что IsLocalPlayer возвращает true
- Проверьте, что Virtual Camera имеет правильный Priority
- Убедитесь, что CinemachineBrain включен

**Растяжение камеры при движении:**
- Увеличьте значение Damping в Transposer
- Уменьшите Follow Distance

**Камера задергивается:**
- Проверьте UpdateMode (должен быть LateUpdate)
- Уменьшите значения Damping

**Shake не работает:**
- Убедитесь, что у Virtual Camera есть Noise компонент
- Проверьте, что CameraManager правильно ссылается на Virtual Camera

## 📝 Дополнительно

- Система полностью совместима с Netcode for GameObjects
- Использует input system локально (не синхронизирует по сети)
- Все эффекты – локальные, не влияют на других игроков
- Поддерживает множество одновременно активных Virtual Cameras

## Лицензия
Используйте свободно в своих проектах!
