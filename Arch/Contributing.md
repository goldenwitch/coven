# Contributing

Feel free to fork and open PRs. If you are adding a new feature please open an issue first. Starting with a problem makes solutions smell better.

If someone has multiple contributions they would like to make to this repo reach out to me directly and I'll do my best to clear out any friction.

### Contribution rules
- Compilation time validation is not optional. If you really want to add a clever feature use Roslyn to make it so we still get compilation time validation.
- We could enable users to define a configuration file somewhere that turns into everything we have here. See above rule.
- We use a PR gate to validate test coverage, test passing, and test run time. Keep tests deterministic and fast running.
- I have linting preferences. Sorry.
- If you add new features to the core library, please make sure they are represented in new samples.
- Dependency injection and configuration must happen at the very root of integration.

Breaking these rules is fine, we can talk about it in the PR if you've got a good reason.

