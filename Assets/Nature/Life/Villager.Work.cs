using UnityEngine;

// Runtime occupations also apply to villagers already saved in the scene.
public partial class Villager
{
    enum WorkStage { GoingToWork, Working, GoingToMarket, Selling }
    int occupation = -1;
    WorkStage workStage;
    Vector3 workPoint, marketPoint;
    Transform workplace, merchandise, carriedGoods, growing;
    Material workWood, workGreen, workSoil;
    float workTimer, customerCheck;
    int stock, earnings;
    Villager lastCustomer;
    Villager[] customers;
    readonly System.Collections.Generic.List<Material> workMaterials = new System.Collections.Generic.List<Material>();

    string Profession => occupation == 0 ? "Carpenter" : occupation == 1 ? "Crop farmer" : "Tree grower";
    string Product => occupation == 0 ? "table" : occupation == 1 ? "vegetables" : "fruit";

    void SetupOccupation()
    {
        if (role != Role.Wanderer || settlement == null || settlement.title != "Castle courtyard" ||
            settlement.assignedWorkers >= 3)
            return;

        occupation = settlement.assignedWorkers++;
        customers = Object.FindObjectsByType<Villager>(FindObjectsInactive.Exclude);
        gameObject.name += " - " + Profession;
        float angle = settlement.AngleOf(settlement.spots.Length > 1 ? settlement.spots[1] : transform.position);
        angle += occupation * 0.8f;
        workPoint = settlement.RingPoint(angle);
        marketPoint = settlement.RingPoint(angle + Mathf.PI);
        Vector3 outward = (workPoint - settlement.center).normalized;
        outward.y = 0f;
        workplace = new GameObject(Profession + " workplace").transform;
        workplace.SetParent(settlement.transform, true);
        workplace.position = workPoint + outward * 1.3f;
        workplace.rotation = Quaternion.LookRotation(outward);
        workWood = WorkMaterial(new Color(0.43f, 0.27f, 0.13f));
        workGreen = WorkMaterial(new Color(0.29f, 0.52f, 0.13f));
        workSoil = WorkMaterial(new Color(0.24f, 0.13f, 0.07f));
        growing = new GameObject("Growing goods").transform;
        growing.SetParent(workplace, false);
        if (occupation == 0)
        {
            MakeTable(workplace, Vector3.zero, 0.8f);
            WorkPart(workplace, PrimitiveType.Cube, "Timber", new Vector3(0f, 0.9f, 0f), new Vector3(1f, 0.12f, 0.3f), workWood);
        }
        else
        {
            WorkPart(workplace, PrimitiveType.Cube, "Planting bed", Vector3.zero, new Vector3(1.6f, 0.08f, 1.3f), workSoil);
            if (occupation == 1)
            {
                for (int i = 0; i < 6; i++)
                    WorkPart(growing, PrimitiveType.Sphere, "Vegetables", new Vector3((i % 3 - 1) * 0.45f, 0.2f, (i / 3 - 0.5f) * 0.6f), Vector3.one * 0.35f, workGreen);
            }
            else
            {
                WorkPart(growing, PrimitiveType.Cylinder, "Planted tree trunk", new Vector3(0f, 0.65f, 0f), new Vector3(0.12f, 0.65f, 0.12f), workWood);
                WorkPart(growing, PrimitiveType.Sphere, "Fruit tree canopy", new Vector3(0f, 1.5f, 0f), new Vector3(1.1f, 1.2f, 1.1f), workGreen);
                for (int i = 0; i < 4; i++)
                    WorkPart(growing, PrimitiveType.Sphere, "Fruit", new Vector3(Mathf.Cos(i * 1.57f) * 0.45f, 1.5f, Mathf.Sin(i * 1.57f) * 0.45f), Vector3.one * 0.16f, workWood);
            }
            growing.localScale = Vector3.one * 0.15f;
        }

        Transform stall = new GameObject(Profession + " market stand").transform;
        stall.SetParent(workplace, true);
        Vector3 marketOut = (marketPoint - settlement.center).normalized;
        stall.position = marketPoint + marketOut * 1.3f;
        stall.rotation = Quaternion.LookRotation(marketOut);
        MakeTable(stall, Vector3.zero, 0.75f);
        merchandise = new GameObject("Goods for sale").transform;
        merchandise.SetParent(stall, false);
        if (occupation == 0)
            MakeTable(merchandise, new Vector3(0f, 0.85f, 0f), 0.4f);
        else
            for (int i = 0; i < 3; i++)
                WorkPart(merchandise, PrimitiveType.Sphere, Product, new Vector3((i - 1) * 0.3f, 0.9f, 0f), Vector3.one * 0.25f, workGreen);
        merchandise.gameObject.SetActive(false);
        carriedGoods = new GameObject("Carried goods").transform;
        carriedGoods.SetParent(body != null ? body : transform, false);
        WorkPart(carriedGoods, PrimitiveType.Cube, "Delivery crate", new Vector3(0f, 1f, 0.4f), new Vector3(0.55f, 0.4f, 0.4f), workWood);
        carriedGoods.gameObject.SetActive(false);
        BeginWorkRoute(workPoint);
    }

    void BeginWorkRoute(Vector3 target)
    {
        settlement.RouteTo(transform.position, target, route);
        routeIndex = 0;
    }

    bool TickOccupation(float dt)
    {
        if (workStage == WorkStage.GoingToWork || workStage == WorkStage.GoingToMarket)
        {
            if (routeIndex < route.Count)
            {
                if (MoveToward(route[routeIndex], dt)) routeIndex++;
                return true;
            }
            workTimer = 0f;
            if (workStage == WorkStage.GoingToWork) workStage = WorkStage.Working;
            else
            {
                workStage = WorkStage.Selling;
                carriedGoods.gameObject.SetActive(false);
                merchandise.gameObject.SetActive(true);
                lastCustomer = null;
            }
        }
        workTimer += dt;
        if (workStage == WorkStage.Working)
        {
            Vector3 face = workplace.position - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            if (occupation != 0) growing.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, workTimer / 24f);
            if (workTimer >= 24f)
            {
                stock = occupation == 0 ? 1 : 3;
                carriedGoods.gameObject.SetActive(true);
                workStage = WorkStage.GoingToMarket;
                BeginWorkRoute(marketPoint);
            }
        }
        else if (workStage == WorkStage.Selling && Time.time >= customerCheck)
        {
            customerCheck = Time.time + 2f;
            // An actual passing villager buys a unit; no sales happen without a customer.
            foreach (Villager buyer in customers)
            {
                if (buyer == null || buyer == this || buyer == lastCustomer || buyer.occupation >= 0 || buyer.role != Role.Wanderer ||
                    buyer.settlement != settlement || (buyer.transform.position - transform.position).sqrMagnitude > 16f)
                    continue;
                stock--;
                earnings += occupation == 0 ? 8 : 2;
                lastCustomer = buyer;
                break;
            }
            if (stock <= 0)
            {
                merchandise.gameObject.SetActive(false);
                if (occupation != 0) growing.localScale = Vector3.one * 0.15f;
                workStage = WorkStage.GoingToWork;
                BeginWorkRoute(workPoint);
            }
        }
        return false;
    }

    void AnimateOccupation(bool walking)
    {
        if (occupation < 0 || walking || workStage != WorkStage.Working || armR == null) return;
        float stroke = Mathf.Sin(Time.time * (occupation == 0 ? 7f : 3f));
        armR.localRotation = Quaternion.Euler(-55f + stroke * 30f, 0f, 0f);
        if (armL != null) armL.localRotation = Quaternion.Euler(-45f, 0f, -8f);
    }

    void OnGUI()
    {
        if (occupation < 0 || !visible || Inventory.IsOpen || Camera.main == null ||
            (PlayerView.Position() - transform.position).sqrMagnitude > 64f) return;
        Vector3 p = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
        if (p.z <= 0f) return;
        string action = workStage == WorkStage.Working ? (occupation == 0 ? "Crafting a table" : "Tending plants") :
            workStage == WorkStage.Selling ? "Selling " + Product + " (" + stock + ")" :
            workStage == WorkStage.GoingToMarket ? "Delivering to market" : "Going to work";
        GUI.Box(new Rect(p.x - 120f, Screen.height - p.y - 25f, 240f, 54f), Profession + " | Earned " + earnings + " coins\n" + action);
    }

    Material WorkMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        workMaterials.Add(material);
        return material;
    }

    void MakeTable(Transform parent, Vector3 origin, float size)
    {
        WorkPart(parent, PrimitiveType.Cube, "Tabletop", origin + Vector3.up * size, new Vector3(1.4f, 0.12f, 0.8f) * size, workWood);
        for (int i = 0; i < 4; i++)
            WorkPart(parent, PrimitiveType.Cube, "Table leg", origin + new Vector3(i % 2 == 0 ? -0.55f : 0.55f, 0.5f, i < 2 ? -0.28f : 0.28f) * size, new Vector3(0.1f, 1f, 0.1f) * size, workWood);
    }

    static void WorkPart(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    void OnDestroy()
    {
        if (workplace != null) Destroy(workplace.gameObject);
        foreach (Material material in workMaterials) if (material != null) Destroy(material);
    }
}
