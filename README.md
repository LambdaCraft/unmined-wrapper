Wrapper around [Unmined](https://unmined.net/) which handles all three dimensions with some customizations for each.

Example docker run command:

```bash
docker run -it --rm -v <mc world folder>:/world -v <output folder>:/generated -e "FORCE_RE_GEN=false" -e "WORLD_NAME=tech" vkorn/unmined-lambda
```