# Bring the upgrade into your Unity project with GitHub Desktop

Repository: **synchrolord/BURN_I**  
Upgrade branch: **codex/pressure-tool-project-cleanup**  
Your existing project folder: **D:\Unity Projects\BURN_I**

The upgrade is on a separate branch. Opening this guide or viewing the pull request does not change your current Unity project. Once you switch or merge branches in GitHub Desktop, Git updates the files in the linked folder; Unity reads those files when you reopen it. You do not need to copy scripts manually.

## Does it save automatically?

| Action | What it saves |
|---|---|
| Save in Unity / your code editor | Current files on disk. This is not a Git checkpoint. Save scenes and project settings before switching branches. |
| Commit in GitHub Desktop | A named checkpoint of the changes you selected, stored in local Git history. Desktop notices changes automatically but does **not** automatically commit them. |
| Push origin | Sends local commits to GitHub, giving you a remote copy. |
| Fetch origin | Checks for remote changes; it does not merge the upgrade into your current branch. |

Uncommitted changes are not guaranteed to be recoverable. Make small commits whenever something works. GitHub documents the explicit review-and-commit workflow in its [commit guide](https://docs.github.com/en/desktop/making-changes-in-a-branch/committing-and-reviewing-changes-to-your-project-in-github-desktop).

## 1. Make a checkpoint first

1. In Unity, stop Play mode. Save the scene and project, then close Unity. Save open scripts too.
2. In GitHub Desktop, choose **BURN_I** under **Current Repository**. Note your current branch; the examples below use `main`.
3. Review the **Changes** list. Include all current work you want protected: scenes, assets, scripts, `.meta` files, `Packages`, and `ProjectSettings`. Generated `Library`, `Temp`, and `Logs` should remain ignored.
4. Enter a summary such as **Checkpoint before pressure tool upgrade**, then click **Commit to main** (or your current branch). Click **Push origin**.
5. Confirm the checkpoint is visible in **History** and there are no uncommitted game changes left.

This upgrade was prepared from your saved project, including the Level_0 placement/mask/sorting edits and grass assets present at that time. Still commit your current work: you may have made additional edits since that snapshot. Unsaved editor changes could not be included in it.

For an easy bookmark, use **Branch → New Branch** to create `backup/before-pressure-upgrade` from this checkpoint, publish it if desired, then return to `main`. The backup name points to the old state; it does not replace regular commits or pushing them.

## 2. Try the upgrade before merging

Click **Fetch origin**, then select `codex/pressure-tool-project-cleanup` under **Current Branch**. If Desktop asks about uncommitted changes, return to the checkpoint step rather than discarding them.

Reopen the same Unity project folder. Let imports and compilation finish. Open **Assets/Game/Scenes/Levels/Level_0.unity**; the previous path moved during cleanup. Test:

- Movement, jump, wall slide/jump, camera and aiming.
- Left mouse sprays; release stops new shots.
- Q switches between blue water and burning flame; wheel changes nozzle.
- Water moves quickly, hits walls and puts out fire; flame creates fire/smoke.
- With unlimited resources disabled, water and fuel drain separately.

The feature branch contains the project snapshot used for the upgrade. Any newer changes you committed only on main will come together with it at merge time. Test the merged main again afterward.

Changes made during Unity Play mode usually disappear when you stop Play. If you make permanent adjustments to the feature branch, save them, close Unity, and commit them before switching branches.

## 3. Merge using Desktop

Close Unity. Select **main** under **Current Branch**. Fetch and pull if Desktop shows remote updates. Then choose **Branch → Squash and Merge into Current Branch**, select `codex/pressure-tool-project-cleanup`, and click **Squash and merge**. Push the resulting commit with **Push origin**. Squashing makes this upgrade one convenient commit to revert.

A regular merge is also available under **Current Branch → Choose a branch to merge into main**; select the upgrade branch and merge it. Use one merge method. The menu labels and both workflows are documented in [GitHub's synchronization guide](https://docs.github.com/en/desktop/working-with-your-remote-repository-on-github-or-github-enterprise/syncing-your-branch-in-github-desktop).

Reopen Unity, allow the import, and test Level_0 again. You are now working on the upgraded main branch. If you instead merge the pull request on GitHub, return to main and **Pull origin** in Desktop; don't also perform a second squash merge locally.

## If Desktop reports conflicts

A conflict means both branches changed overlapping content. It does not mean the checkpoint is gone. Keep Unity closed during resolution. Scene and prefab files contain object IDs and serialized references; avoid blindly choosing an entire old or new file. The folder moves can also produce rename conflicts when newer edits touch old paths. Resolve the file contents and keep each asset paired with its original `.meta`. Cancel the merge and get help if you're unsure which side contains your latest scene work.

## 4. Undo the upgrade if needed

Save and commit any newer work you want to preserve, then close Unity. In Desktop on main, open **History**, right-click the upgrade's squash commit, and choose **Revert Changes in Commit**. This makes a new commit that reverses the upgrade; the original checkpoint and upgrade remain in history. Push origin, reopen Unity and let it import the restored layout. See [GitHub's revert guide](https://docs.github.com/en/desktop/managing-commits/reverting-a-commit-in-github-desktop).

After reverting the folder moves, the old startup scene path is **Assets/Scenes/Levels/Level_0.unity**. If you used a regular merge or made later overlapping changes, rollback can require resolving conflicts; don't use Discard Changes to try to undo committed history.

To simply inspect the old complete project, switch to `backup/before-pressure-upgrade` after saving/committing your current work. That changes the files in your working folder to the checkpoint. Switching back returns to the newer branch. Reverting one commit reverses that commit's changes; it is not a promise to undo every later edit.

## A useful everyday routine

**Work → stop Play → save → review Changes → commit with a clear name → push.**

Examples: `Working water jet`, `New grass area`, `Adjusted flame lifetime`. Small checkpoints make it much easier to identify and undo the exact change that caused trouble. Keep scene, asset and `.meta` changes together in a commit.
