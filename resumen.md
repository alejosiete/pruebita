# Repositorios remotos

Obtener y Crear repositorios:
Hay dos maneras de obtener un repositorio Git. Una de ellas es copiarlo desde un repositorio existente en la red o en otro lugar y la otra es crear uno nuevo en un directorio existente.

git init
Para tomar un directorio y convertirlo en un nuevo repositorio Git en el que puedas empezar a controlar sus versiones, simplemente puedes ejecutar git init.

git clone <url>
El comando 'git clone` es en realidad una especie de envoltura alrededor de varios otros comandos. Éste crea un nuevo directorio, entra en él y ejecuta git init para que sea un repositorio vacío de Git, añade uno remoto (git remote add) hacia la dirección URL que se le pasa (por defecto llamado origin)

## `git fetch` — traer sin combinar (la versión "más segura")
```bash
git fetch
```

## `git pull` — traer y combinar cambios del remoto
```bash
git pull
```

## `git push` — subir commits locales al remoto
```bash
git push -u origin main
```

**Después de la primera vez:**
```bash
git push
```
