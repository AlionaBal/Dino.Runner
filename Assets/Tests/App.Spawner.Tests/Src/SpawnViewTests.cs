using System.Collections;
using App.GameCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace App.Spawner.Tests
{
    public class SpawnViewTests
    {
        private const int MaxFixedFrames = 100;

        private GameObject _gameObject;
        private StubGameContext _gameContext;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("SpawnView");
            _gameObject.SetActive(false);

            var rigidbody = _gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            _gameObject.AddComponent<BoxCollider>();

            _gameContext = new StubGameContext { Speed = 200f };
            _gameObject.AddComponent<SpawnView>().SetUp(_gameContext, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_gameObject);
        }

        [UnityTest]
        public IEnumerator Spawn_FirstTime_ShouldStartAtSpawnPoint()
        {
            _gameContext.Speed = 1f;

            _gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(_gameObject.activeSelf);
            Assert.Greater(_gameObject.transform.position.x, 9f);
        }

        [UnityTest]
        public IEnumerator Spawn_ReusedFromPool_ShouldRestartAtSpawnPoint()
        {
            // Arrange: spawn and let the view move off-screen until it hides itself (returns to pool)
            _gameObject.SetActive(true);

            for (int i = 0; i < MaxFixedFrames && _gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsFalse(_gameObject.activeSelf, "View should hide itself after leaving the screen");

            // Act: reuse from pool
            _gameContext.Speed = 1f;
            _gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Assert
            Assert.IsTrue(_gameObject.activeSelf, "Reused view should stay visible");
            Assert.Greater(_gameObject.transform.position.x, 9f);
        }

        private class StubGameContext : IGameContext
        {
            public float Speed { get; set; }
        }
    }
}
