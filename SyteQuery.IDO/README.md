# The SyteQuery query IDO

SyteQuery runs your SQL through a small **custom IDO** that you install once in each SyteLine environment you want to query. The IDO is a short .NET class: it takes a SQL command, runs it against the SyteLine database, and hands the rows back as JSON. Nothing else in SyteLine is touched.

> **Prerequisite:** this IDO must be installed in a SyteLine environment *before* that environment can be added to SyteQuery or queried. Adding an environment runs a connection check through the IDO and is refused until it works.

SyteQuery does **not** install the IDO for you. The IDO is source code in this folder, so the overall process is:

1. **Compile** the `SyteQuery.IDO` project (see [Build](#3-build)). This produces a **DLL and a PDB**.
2. **Install both files** in SyteLine on the **IDO Extension Class Assemblies** form.
3. **Create a new IDO** (we suggest a dedicated one, e.g. `ue_QueryTool`), **bind the assembly** to it as its extension class, and **set up the `ExecuteQuery` IDO method**.
4. **Tell SyteQuery the IDO's name** when you add the environment (**Tools → Environments → Add → IDO name**).

This is the standard SyteLine administration process for custom extension classes, and it means you decide exactly what runs on your server and can read every line of it first. Using a new, dedicated IDO (rather than adding the method to an existing one) keeps it isolated and easy to secure, review or remove.

> The IDO's source is in this folder (`SyteQuery.IDO/`). This guide covers what SyteQuery expects of it, how to build it, how to install it, and how to fix the usual problems.

---

## 1. What SyteQuery expects (the contract)

| | |
|---|---|
| **IDO name** | Anything you like — you enter it per environment in SyteQuery. Custom IDOs conventionally start with `ue_` (for example `ue_QueryTool`). |
| **Method name** | `ExecuteQuery` — fixed; SyteQuery always calls this name. |
| **Parameters** (in this order) | |
| 1. `InputCommand` | **Input.** The SQL text to run. |
| 2. `Output` | **Output.** The result rows as a JSON array of objects, e.g. `[{"ItemCode":"A100","Qty":4}]` — either plain JSON or the same JSON base64-encoded (UTF-8); the reference implementation returns base64. Empty when the command returns no rows. |
| 3. `Infobar` | **Input / output / message.** Status text. When the SQL fails, put the SQL error message here and leave `Output` empty — SyteQuery shows it in the red error banner. |
| **Return value** | `0` on success; non-zero after setting `Infobar` on failure (the reference implementation returns `16`). |

When you add an environment, SyteQuery checks the IDO end to end by running `SELECT 1 AS ok` through it and expecting `[{"ok":1}]` back. If anything is off, it tells you what (see [Troubleshooting](#5-troubleshooting)).

The reference implementation is in [`QueryTool.cs`](QueryTool.cs): extension class `QueryTool` in namespace `QueryTool`, with the `ExecuteQuery` method above.

### Reference settings

These are the settings of the IDO SyteQuery was developed against. Screens and field names differ between SyteLine versions, but these are the values that matter:

| Setting | Reference value | Notes |
|---|---|---|
| Access As | `ue_` | The usual prefix for customer-created objects. |
| Extension class namespace | `QueryTool` | Must match the class in the assembly. |
| Extension class name | `QueryTool` | Must match the class in the assembly. |
| Extends | `ReportDataViews` | An IDO that doesn't require a table. Use whatever your site normally extends for method-only IDOs. |
| Server name | your site's IDO server | The reference environment used a custom one (`RC_IDO`); use the server your other custom IDOs use. |
| Quote table aliases | on | |
| Method type | extension-class method | |
| Parameter types | text / variant | `InputCommand` in; `Output` out; `Infobar` in/out/message. |

---

## 2. Security — read this before installing

The IDO runs **arbitrary SQL with the permissions of SyteLine's database login**. That is the whole point, and also the risk.

- SyteQuery blocks `CREATE`/`ALTER`/`DROP` of procedures, views, functions and triggers *in the client*, but the IDO itself does not enforce anything — a different client calling the same IDO is not subject to those checks. **The server is the only real boundary.**
- Use SyteLine's own security to limit who may execute this IDO (user groups / IDO authorization) to the people who should have this power. Treat it like granting direct database access.
- Install it in a **test environment first**.
- If you want a read-only tool, enforce that in the IDO's code (for example, reject anything that isn't a `SELECT`, or run it under a read-only connection) rather than relying on SyteQuery.

---

## 3. Build

You need:

- **Visual Studio 2022** (or the Build Tools) with the **.NET Framework 4.7.2 targeting pack**.
- From a SyteLine installation of the **same version** you will deploy to: Infor's **`IDOCore.dll`** and **`MGShared.dll`**, plus the **`Newtonsoft.Json.dll`** that SyteLine itself loads. The Infor files **cannot be committed to this repository or redistributed**, so the project must reference copies on your own machine instead — and the assemblies are not copied to the output (SyteLine already has them).

Point the project at those copies with a property, so nothing machine-specific is committed. In `SyteQuery.IDO.csproj`:

```xml
<ItemGroup>
  <Reference Include="IDOCore">
    <HintPath>$(InforBinPath)\IDOCore.dll</HintPath>
    <Private>False</Private>
  </Reference>
  <Reference Include="MGShared">
    <HintPath>$(InforBinPath)\MGShared.dll</HintPath>
    <Private>False</Private>
  </Reference>
  <Reference Include="Newtonsoft.Json">
    <HintPath>$(InforBinPath)\Newtonsoft.Json.dll</HintPath>
    <Private>False</Private>
  </Reference>
</ItemGroup>
```

Then build the **Release** configuration, passing the folder that holds those files:

```
msbuild SyteQuery.IDO\SyteQuery.IDO.csproj /p:Configuration=Release /p:InforBinPath="C:\path\to\your\syteline\bin"
```

You get two files in `SyteQuery.IDO\bin\Release\`:

```
SyteQuery.IDO.dll
SyteQuery.IDO.pdb
```

**SyteLine requires both** when you save an assembly, so keep both. To stop them recording your local folder names and Windows user name, build deterministically with mapped source paths — in the project file:

```xml
<PropertyGroup>
  <Deterministic>true</Deterministic>
  <PathMap>$(MSBuildProjectDirectory)=/src/SyteQuery.IDO</PathMap>
</PropertyGroup>
```

Before publishing or sharing a build, check it: open the DLL and PDB in a text editor (or run `strings` over them) and make sure no path on your machine, and nothing containing your Windows user name, appears anywhere.

---

## 4. Install in SyteLine

> Screen names below follow common SyteLine / Mongoose Object Studio naming; yours may differ slightly by version.

1. **Install the assembly (DLL and PDB).** Open the **IDO Extension Class Assemblies** form and add a new assembly record (for example `ue_QueryTool`, *Access As* `ue_`). Upload **both** the compiled `SyteQuery.IDO.dll` and its `SyteQuery.IDO.pdb` (SyteLine requires the pair). Save.
2. **Create a new IDO and bind the assembly to it.** We recommend a new, dedicated IDO rather than adding the method to an existing one. In the **IDOs** form, create the IDO (for example `ue_QueryTool`) using the [reference settings](#reference-settings), and on its extension-class setup bind the assembly from step 1 with namespace `QueryTool` and class `QueryTool`.
3. **Set up the `ExecuteQuery` IDO method.** On the new IDO, add a method named **`ExecuteQuery`** (extension-class method) with the three parameters from the [contract](#1-what-sytequery-expects-the-contract), in order: `InputCommand` (input), `Output` (output), `Infobar` (input, output, message).
4. **Save and reload.** Save the IDO, then reload the IDO metadata (or recycle the application pool) so the new IDO is picked up.
5. **Grant access.** Authorize only the users or groups that should be able to run it (see [Security](#2-security--read-this-before-installing)).
6. **Add the environment in SyteQuery.** *Tools → Environments → Add* — enter the URL, configuration, credentials, and the **IDO name** from step 2. SyteQuery runs its end-to-end check and tells you if anything is wrong.

---

## 5. Troubleshooting

What SyteQuery says, and what to look at:

| Message | Likely cause |
|---|---|
| *SyteLine doesn't know an IDO named '…'* | The name doesn't match the IDO exactly, the IDO wasn't saved, or the metadata cache hasn't been reloaded (step 4). |
| *SyteLine rejected the call to …ExecuteQuery* | The IDO has no method called `ExecuteQuery`, it has the wrong parameters, or the assembly isn't bound to the IDO. |
| *ran but returned no data* | The method runs but doesn't put JSON rows in its second (`Output`) parameter. |
| *answered with an error instead of data: …* | The method ran and reported a failure in `Infobar` — the text is the underlying error (often a SQL or permissions problem). |
| *returned data, but not a JSON array of rows* | `Output` isn't a JSON array of objects (or its base64). |
| Authentication failed | Not an IDO problem — check URL, configuration, user and password. |

Still stuck? Open an issue (without credentials, tenant URLs or customer data).
