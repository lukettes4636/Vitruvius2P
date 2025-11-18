using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [Tooltip("El dano que inflige este objeto (90 para el enemigo).")]
    [SerializeField] private int damageAmount = 90;

    private void OnTriggerEnter(Collider other)
    {
        
        if (!gameObject.activeInHierarchy) return;

        
        
        
        
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        
        if (playerHealth != null && !playerHealth.IsDead)
        {
            
            if (other.gameObject.name == "Player1" || other.gameObject.name == "Player2")
            {
                playerHealth.SetLastDamageSource("EnemyDamageDealer");
                playerHealth.TakeDamage(damageAmount);

            }
        }
    }

    
    public void SetDamage(int damage)
    {
        damageAmount = damage;
    }
}
