using UnityEngine;

public enum MaterialType
{
    Fibre,
    Plastics,
    Aluminium,
    Steel,
    Residue
}

public enum PayType
{
    Purchase,
    Maintenance,
    Ship,
    Dump
}

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("Starting Money")]
    public int startingCoins = 5000;

    [Header("Material Values (coins per tonne)")]
    public int fibrePerTonne = 20;
    public int plasticsPerTonne = 40;
    public int steelPerTonne = 80;
    public int aluminiumPerTonne = 150;

    [Header("Residue")]
    public int dumpCostPerDump = 100;

    [Header("Contamination")]
    [Tooltip("Each 1% contamination reduces value by this fraction (0.1 = 10% per 1%)")]
    public float valueLossPerPercent = 0.1f;

    public int CurrentCoins { get; private set; }
    // Compatibility property so UI or gameplay scripts can read money as "CurrentMoney"
    public int CurrentMoney => CurrentCoins;
    // newBalance, delta, payType, label
    public event System.Action<int, int, PayType, string> OnBalanceChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentCoins = startingCoins;
        OnBalanceChanged?.Invoke(CurrentCoins, 0, PayType.Purchase, "Init");
    }

    // -------- Purchasing --------

    public bool CanAfford(int cost)
    {
        return cost >= 0 && CurrentCoins >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost)) return false;
        CurrentCoins -= cost;
        OnBalanceChanged?.Invoke(CurrentCoins, -cost, PayType.Purchase, "Spend");
        return true;
    }

    /// <summary>
    /// Newer API used by PackingArea. Credits money for shipping and returns the credited payout.
    /// </summary>
    public int CreditShipment(MaterialType material, float tonnes, float contaminationRate)
    {
        return Ship(material, tonnes, contaminationRate);
    }

    /// <summary>
    /// Overload used by PackingArea: allows tagging the spend with a transaction type and label.
    /// </summary>
    public bool TrySpend(int cost, PayType type, string label)
    {
        if (!CanAfford(cost)) return false;
        CurrentCoins -= cost;
        OnBalanceChanged?.Invoke(CurrentCoins, -cost, type, label);
        return true;
    }

    public bool TryPurchase(int cost, string machineName)
    {
        return TrySpend(cost, PayType.Purchase, machineName);
    }

    public int SellBack(int purchasePrice, float resaleRate = 0.5f)
    {
        int refund = Mathf.RoundToInt(purchasePrice * resaleRate);
        CurrentCoins += refund;
        return refund;
    }

    // -------- Shipping --------

    public int Ship(MaterialType material, float tonnes, float contaminationRate)
    {
        if (material == MaterialType.Residue) return 0;
        if (tonnes <= 0f) return 0;

        int pricePerTonne = GetCoinsPerTonne(material);

        float contaminationPercent = Mathf.Clamp01(contaminationRate) * 100f;
        float multiplier = 1f - (contaminationPercent * valueLossPerPercent);
        multiplier = Mathf.Clamp01(multiplier);

        int payout = Mathf.RoundToInt(tonnes * pricePerTonne * multiplier);
        if (payout <= 0) return 0;

        CurrentCoins += payout;
        OnBalanceChanged?.Invoke(CurrentCoins, payout, PayType.Ship, material.ToString());
        return payout;
    }

    // -------- Residue --------

    public bool CanAffordDump()
    {
        return dumpCostPerDump <= 0 || CurrentCoins >= dumpCostPerDump;
    }

    public bool TryDumpResidue()
    {
        if (!CanAffordDump()) return false;
        CurrentCoins -= dumpCostPerDump;
        OnBalanceChanged?.Invoke(CurrentCoins, -dumpCostPerDump, PayType.Dump, "Dump");
        return true;
    }

    // -------- Helpers --------

    private int GetCoinsPerTonne(MaterialType material)
    {
        switch (material)
        {
            case MaterialType.Fibre: return fibrePerTonne;
            case MaterialType.Plastics: return plasticsPerTonne;
            case MaterialType.Steel: return steelPerTonne;
            case MaterialType.Aluminium: return aluminiumPerTonne;
            default: return 0;
        }
    }
}