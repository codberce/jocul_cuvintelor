using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WordGame.Application.Options;
using WordGame.Application.Subjects;
using WordGame.Domain.Entities;
using WordGame.Domain.Enums;
using WordGame.Infrastructure.Identity;

namespace WordGame.Infrastructure.Persistence;

public sealed class DatabaseSeeder(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<SecurityOptions> securityOptions)
{
    private readonly SecurityOptions _security = securityOptions.Value;

    public async Task MigrateAndSeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedIdentityAsync();
        await SeedContentAsync(cancellationToken);
    }

    private async Task SeedIdentityAsync()
    {
        foreach (var role in new[] { "Admin", "Host" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        await EnsureUserAsync(_security.AdminEmail, _security.AdminPassword, "Administrator", "Admin", "Host");
        await EnsureUserAsync(_security.HostEmail, _security.HostPassword, "Gazda", "Host");
    }

    private async Task EnsureUserAsync(string email, string password, string displayName, params string[] roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
            }
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private async Task SeedContentAsync(CancellationToken cancellationToken)
    {
        var categories = new[]
        {
            new Category { Name = "Vocabular", Slug = "vocabular" },
            new Category { Name = "Gramatica", Slug = "gramatica" },
            new Category { Name = "Literatura", Slug = "literatura" },
            new Category { Name = "Cultura generala", Slug = "cultura-generala" },
            new Category { Name = "Stiinte", Slug = "stiinte" },
            new Category { Name = "Matematica", Slug = "matematica" },
            new Category { Name = "Informatica", Slug = "informatica" },
            new Category { Name = "Istorie", Slug = "istorie" },
            new Category { Name = "Fizica", Slug = "fizica" },
            new Category { Name = "Geografie", Slug = "geografie" },
            new Category { Name = "Biologie", Slug = "biologie" },
            new Category { Name = "Chimie", Slug = "chimie" },
            new Category { Name = "Religie", Slug = "religie" },
            new Category { Name = "Economie", Slug = "economie" },
            new Category { Name = "Engleza", Slug = "engleza" },
            new Category { Name = "Franceza", Slug = "franceza" },
            new Category { Name = "Germana", Slug = "germana" }
        };

        foreach (var subjectOption in SubjectCatalog.Subjects)
        {
            var subject = await dbContext.Subjects.SingleOrDefaultAsync(x => x.Slug == subjectOption.Slug, cancellationToken);
            if (subject is null)
            {
                dbContext.Subjects.Add(new Subject
                {
                    Name = subjectOption.Name,
                    Slug = subjectOption.Slug,
                    TargetQuestionCount = SubjectCatalog.TargetQuestionCount
                });
            }
            else
            {
                subject.Name = subjectOption.Name;
                subject.TargetQuestionCount = SubjectCatalog.TargetQuestionCount;
                subject.IsActive = true;
            }
        }

        foreach (var category in categories)
        {
            if (!await dbContext.Categories.AnyAsync(x => x.Slug == category.Slug, cancellationToken))
            {
                dbContext.Categories.Add(category);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var categoryMap = await dbContext.Categories.ToDictionaryAsync(x => x.Slug, x => x, cancellationToken);
        var subjectMap = await dbContext.Subjects.ToDictionaryAsync(x => x.Slug, x => x, cancellationToken);
        await SeedLessonsAsync(categoryMap, cancellationToken);
        await SeedQuestionsAsync(categoryMap, subjectMap, cancellationToken);
    }

    private async Task SeedLessonsAsync(Dictionary<string, Category> categories, CancellationToken cancellationToken)
    {
        var lessons = new[]
        {
            new Lesson { Title = "Normalizarea raspunsurilor", Slug = "normalizarea-raspunsurilor", DisplayOrder = 1, CategoryId = categories["vocabular"].Id, Content = "In joc, raspunsurile sunt evaluate dupa forma lor normalizata: litere mici, fara diacritice, fara punctuatie simpla si fara spatii inutile. Astfel, SCOALA si scoala sunt echivalente." },
            new Lesson { Title = "Definitia si termenul", Slug = "definitia-si-termenul", DisplayOrder = 2, CategoryId = categories["vocabular"].Id, Content = "O definitie buna identifica sensul esential al unui cuvant. Jucatorul trebuie sa deduca termenul exact pornind de la indicii precum categoria, dificultatea si numarul de litere." },
            new Lesson { Title = "Strategii de raspuns rapid", Slug = "strategii-raspuns-rapid", DisplayOrder = 3, CategoryId = categories["gramatica"].Id, Content = "Scorul combina lungimea raspunsului corect cu bonusul de viteza. Este important sa raspunzi corect, dar si sa confirmi raspunsul inainte ca timpul sa expire." }
        };

        foreach (var lesson in lessons)
        {
            if (!await dbContext.Lessons.AnyAsync(x => x.Slug == lesson.Slug, cancellationToken))
            {
                dbContext.Lessons.Add(lesson);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedQuestionsAsync(Dictionary<string, Category> categories, Dictionary<string, Subject> subjects, CancellationToken cancellationToken)
    {
        foreach (var seed in SeedQuestions.Concat(SubjectSeedQuestions))
        {
            var category = categories[seed.CategorySlug];
            var subject = subjects[seed.SubjectSlug];
            var exists = await dbContext.Questions.AnyAsync(
                x => x.CategoryId == category.Id && x.Answer == seed.Answer,
                cancellationToken);

            if (exists)
            {
                var existing = await dbContext.Questions.FirstAsync(x => x.CategoryId == category.Id && x.Answer == seed.Answer, cancellationToken);
                if (existing.SubjectId is null)
                {
                    existing.SubjectId = subject.Id;
                }
                continue;
            }

            dbContext.Questions.Add(new Question
            {
                Answer = seed.Answer,
                Definition = seed.Definition,
                SubjectId = subject.Id,
                CategoryId = category.Id,
                Difficulty = seed.Difficulty,
                TimeLimitSeconds = seed.TimeLimitSeconds,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record QuestionSeed(string Answer, string Definition, string CategorySlug, Difficulty Difficulty, int TimeLimitSeconds = 25, string SubjectSlug = "romanian");

    private static readonly QuestionSeed[] SeedQuestions =
    [
        new("scoala", "Institutia in care elevii invata organizat sub indrumarea profesorilor.", "vocabular", Difficulty.Easy),
        new("biblioteca", "Locul in care sunt pastrate si imprumutate carti.", "vocabular", Difficulty.Easy),
        new("dictionar", "Lucrare care explica sensul cuvintelor unei limbi.", "vocabular", Difficulty.Easy),
        new("sinonim", "Cuvant cu sens asemanator altui cuvant.", "vocabular", Difficulty.Easy),
        new("antonim", "Cuvant cu sens opus altui cuvant.", "vocabular", Difficulty.Easy),
        new("metafora", "Figura de stil bazata pe transferul de sens prin asemanare.", "vocabular", Difficulty.Medium),
        new("epitet", "Determinant expresiv care evidentiaza o insusire.", "vocabular", Difficulty.Medium),
        new("proverb", "Enunt popular scurt care exprima o invatatura.", "vocabular", Difficulty.Easy),
        new("arhaism", "Cuvant vechi, iesit din uzul curent.", "vocabular", Difficulty.Medium),
        new("neologism", "Cuvant nou intrat recent in limba.", "vocabular", Difficulty.Medium),
        new("polisemie", "Proprietatea unui cuvant de a avea mai multe sensuri.", "vocabular", Difficulty.Hard),
        new("omonim", "Cuvant cu aceeasi forma ca altul, dar cu sens diferit.", "vocabular", Difficulty.Medium),
        new("regionalism", "Cuvant folosit mai ales intr-o anumita zona geografica.", "vocabular", Difficulty.Medium),
        new("expresie", "Grup stabil de cuvinte cu sens unitar.", "vocabular", Difficulty.Easy),
        new("definitie", "Explicatie concisa a sensului unui termen.", "vocabular", Difficulty.Easy),
        new("vocabular", "Totalitatea cuvintelor cunoscute sau folosite de cineva.", "vocabular", Difficulty.Easy),
        new("sens", "Intelesul unui cuvant sau al unei propozitii.", "vocabular", Difficulty.Easy),
        new("context", "Ansamblul de cuvinte sau situatii care clarifica sensul.", "vocabular", Difficulty.Easy),
        new("nuanta", "Diferenta fina de inteles sau expresie.", "vocabular", Difficulty.Medium),
        new("derivare", "Formarea unui cuvant nou cu prefixe sau sufixe.", "vocabular", Difficulty.Medium),
        new("substantiv", "Partea de vorbire care denumeste fiinte, lucruri sau fenomene.", "gramatica", Difficulty.Easy),
        new("adjectiv", "Partea de vorbire care exprima insusirea unui substantiv.", "gramatica", Difficulty.Easy),
        new("verb", "Partea de vorbire care exprima actiunea, starea sau existenta.", "gramatica", Difficulty.Easy),
        new("pronume", "Partea de vorbire care tine locul unui substantiv.", "gramatica", Difficulty.Easy),
        new("adverb", "Partea de vorbire care determina un verb, adjectiv sau alt adverb.", "gramatica", Difficulty.Medium),
        new("prepozitie", "Partea de vorbire care leaga un complement de termenul determinat.", "gramatica", Difficulty.Medium),
        new("conjunctie", "Partea de vorbire care leaga propozitii sau parti de propozitie.", "gramatica", Difficulty.Medium),
        new("interjectie", "Partea de vorbire care exprima stari, indemnuri sau sunete.", "gramatica", Difficulty.Medium),
        new("predicat", "Partea principala de propozitie care arata ce face subiectul.", "gramatica", Difficulty.Easy),
        new("subiect", "Partea principala de propozitie despre care se spune ceva.", "gramatica", Difficulty.Easy),
        new("atribut", "Partea secundara de propozitie care determina un substantiv.", "gramatica", Difficulty.Medium),
        new("complement", "Partea secundara de propozitie care determina de obicei un verb.", "gramatica", Difficulty.Medium),
        new("acuzativ", "Caz gramatical folosit frecvent pentru complementul direct.", "gramatica", Difficulty.Medium),
        new("genitiv", "Caz care exprima posesia sau apartenenta.", "gramatica", Difficulty.Medium),
        new("dativ", "Caz care indica destinatarul actiunii.", "gramatica", Difficulty.Medium),
        new("vocativ", "Caz al adresarii directe.", "gramatica", Difficulty.Easy),
        new("nominativ", "Caz al subiectului si al numelui predicativ.", "gramatica", Difficulty.Medium),
        new("diateza", "Categorie verbala care arata raportul dintre actiune si subiect.", "gramatica", Difficulty.Hard),
        new("conjugare", "Modificarea verbului dupa persoana, numar, timp si mod.", "gramatica", Difficulty.Medium),
        new("declinare", "Modificarea substantivului, adjectivului sau pronumelui dupa caz.", "gramatica", Difficulty.Hard),
        new("roman", "Specie epica ampla, cu actiune complexa si multe personaje.", "literatura", Difficulty.Easy),
        new("nuvela", "Specie epica in proza, mai scurta decat romanul.", "literatura", Difficulty.Easy),
        new("basm", "Naratiune fantastica in care binele invinge raul.", "literatura", Difficulty.Easy),
        new("balada", "Creatie epica in versuri cu actiune dramatica.", "literatura", Difficulty.Medium),
        new("doina", "Creatie lirica populara care exprima dor, jale sau iubire.", "literatura", Difficulty.Medium),
        new("elegie", "Poezie lirica dominata de tristete si meditatie.", "literatura", Difficulty.Medium),
        new("comedie", "Specie dramatica ce provoaca rasul si critica defecte.", "literatura", Difficulty.Easy),
        new("tragedie", "Specie dramatica avand conflict grav si final nefericit.", "literatura", Difficulty.Medium),
        new("narator", "Vocea care relateaza intamplarile intr-un text epic.", "literatura", Difficulty.Easy),
        new("personaj", "Fiinta imaginara sau reala care participa la actiunea literara.", "literatura", Difficulty.Easy),
        new("intriga", "Momentul care declanseaza conflictul intr-o opera epica sau dramatica.", "literatura", Difficulty.Medium),
        new("expozitiune", "Partea initiala care prezinta cadrul si personajele.", "literatura", Difficulty.Medium),
        new("deznodamant", "Partea finala in care conflictul se rezolva.", "literatura", Difficulty.Medium),
        new("liric", "Gen literar in care autorul isi exprima direct sentimentele.", "literatura", Difficulty.Easy),
        new("epic", "Gen literar bazat pe naratiune si actiune.", "literatura", Difficulty.Easy),
        new("dramatic", "Gen literar destinat reprezentarii pe scena.", "literatura", Difficulty.Easy),
        new("rima", "Potrivirea sunetelor la final de vers.", "literatura", Difficulty.Easy),
        new("ritm", "Succesiunea regulata a silabelor accentuate si neaccentuate.", "literatura", Difficulty.Medium),
        new("strofa", "Grup de versuri intr-o poezie.", "literatura", Difficulty.Easy),
        new("monolog", "Replica ampla rostita de un singur personaj.", "literatura", Difficulty.Medium),
        new("carpati", "Lantul muntos care traverseaza centrul Romaniei.", "cultura-generala", Difficulty.Easy),
        new("dunare", "Fluviul care formeaza o parte din granita sudica a Romaniei.", "cultura-generala", Difficulty.Easy),
        new("bucuresti", "Capitala Romaniei.", "cultura-generala", Difficulty.Easy),
        new("unire", "Proces politic prin care doua sau mai multe teritorii devin unul singur.", "cultura-generala", Difficulty.Medium),
        new("constitutie", "Legea fundamentala a unui stat.", "cultura-generala", Difficulty.Medium),
        new("democratie", "Forma de guvernare bazata pe participarea cetatenilor.", "cultura-generala", Difficulty.Medium),
        new("patrimoniu", "Totalitatea bunurilor culturale mostenite si protejate.", "cultura-generala", Difficulty.Medium),
        new("traditie", "Obicei transmis din generatie in generatie.", "cultura-generala", Difficulty.Easy),
        new("folclor", "Totalitatea creatiilor populare ale unei comunitati.", "cultura-generala", Difficulty.Medium),
        new("cetate", "Constructie fortificata destinata apararii.", "cultura-generala", Difficulty.Easy),
        new("muzeu", "Institutia care conserva si expune obiecte valoroase.", "cultura-generala", Difficulty.Easy),
        new("harta", "Reprezentare grafica a unei suprafete geografice.", "cultura-generala", Difficulty.Easy),
        new("continent", "Intindere foarte mare de uscat inconjurata de ape.", "cultura-generala", Difficulty.Easy),
        new("capitala", "Orasul principal in care se afla conducerea unui stat.", "cultura-generala", Difficulty.Easy),
        new("istorie", "Stiinta care studiaza trecutul omenirii.", "cultura-generala", Difficulty.Easy),
        new("civilizatie", "Nivelul de dezvoltare materiala si spirituala a unei societati.", "cultura-generala", Difficulty.Medium),
        new("republica", "Forma de guvernare in care seful statului este ales.", "cultura-generala", Difficulty.Medium),
        new("monarhie", "Forma de guvernare condusa de un rege sau imparat.", "cultura-generala", Difficulty.Medium),
        new("frontiera", "Linie care delimiteaza teritoriul unui stat.", "cultura-generala", Difficulty.Medium),
        new("cetatean", "Persoana care apartine juridic unui stat.", "cultura-generala", Difficulty.Easy),
        new("atom", "Cea mai mica unitate a unui element chimic care pastreaza proprietatile acestuia.", "stiinte", Difficulty.Medium),
        new("molecula", "Grup de atomi legati chimic.", "stiinte", Difficulty.Medium),
        new("energie", "Capacitatea unui sistem de a efectua lucru mecanic sau de a produce schimbare.", "stiinte", Difficulty.Medium),
        new("gravitate", "Forta de atractie dintre corpuri cu masa.", "stiinte", Difficulty.Medium),
        new("ecosistem", "Ansamblu format din organisme si mediul lor de viata.", "stiinte", Difficulty.Medium),
        new("fotosinteza", "Procesul prin care plantele produc substante organice folosind lumina.", "stiinte", Difficulty.Medium),
        new("celula", "Unitatea structurala si functionala de baza a organismelor vii.", "stiinte", Difficulty.Easy),
        new("tesut", "Grup de celule cu structura si functie asemanatoare.", "stiinte", Difficulty.Medium),
        new("organ", "Parte a corpului cu forma si functie determinate.", "stiinte", Difficulty.Easy),
        new("oxigen", "Gaz necesar respiratiei majoritatii organismelor.", "stiinte", Difficulty.Easy),
        new("hidrogen", "Cel mai usor element chimic.", "stiinte", Difficulty.Medium),
        new("carbon", "Element chimic esential in compusii organici.", "stiinte", Difficulty.Easy),
        new("magnetism", "Fenomen fizic asociat campurilor magnetice.", "stiinte", Difficulty.Medium),
        new("electricitate", "Ansamblul fenomenelor produse de sarcini electrice.", "stiinte", Difficulty.Medium),
        new("temperatura", "Marime fizica ce exprima starea termica a unui corp.", "stiinte", Difficulty.Easy),
        new("presiune", "Marime fizica egala cu forta exercitata pe unitatea de suprafata.", "stiinte", Difficulty.Medium),
        new("volum", "Spatiul ocupat de un corp.", "stiinte", Difficulty.Easy),
        new("densitate", "Raportul dintre masa si volum.", "stiinte", Difficulty.Medium),
        new("reactie", "Transformare chimica in care se formeaza substante noi.", "stiinte", Difficulty.Medium),
        new("ipoteza", "Presupunere formulata pentru a fi verificata stiintific.", "stiinte", Difficulty.Medium),
        new("algoritm", "Sir finit de pasi pentru rezolvarea unei probleme.", "stiinte", Difficulty.Medium),
        new("variabila", "Marime sau valoare care se poate schimba.", "stiinte", Difficulty.Easy),
        new("functie", "Relatie care asociaza fiecarui element un rezultat.", "stiinte", Difficulty.Medium),
        new("fract ie".Replace(" ", ""), "Numar care exprima una sau mai multe parti egale dintr-un intreg.", "stiinte", Difficulty.Easy),
        new("procent", "Raport exprimat la o suta.", "stiinte", Difficulty.Easy),
        new("simetrie", "Corespondenta armonioasa intre partile unei figuri.", "stiinte", Difficulty.Medium),
        new("unghi", "Figura formata de doua semidrepte cu aceeasi origine.", "stiinte", Difficulty.Easy),
        new("triunghi", "Poligon cu trei laturi.", "stiinte", Difficulty.Easy),
        new("cerc", "Multimea punctelor aflate la aceeasi distanta de un centru.", "stiinte", Difficulty.Easy),
        new("diametru", "Coarda care trece prin centrul cercului.", "stiinte", Difficulty.Medium)
    ];

    private static readonly QuestionSeed[] SubjectSeedQuestions =
    [
        new("derivata", "Limita raportului incremental care masoara rata instantanee de variatie.", "matematica", Difficulty.Hard, SubjectSlug: "math"),
        new("integrala", "Operatie inversa derivarii, folosita pentru arii si acumulari.", "matematica", Difficulty.Hard, SubjectSlug: "math"),
        new("matrice", "Tablou dreptunghiular de numere organizate pe linii si coloane.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("vector", "Marime caracterizata prin modul, directie si sens.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("functie", "Relatie care asociaza fiecarui element din domeniu un singur rezultat.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("logaritm", "Exponentul la care trebuie ridicata baza pentru a obtine un numar.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("parabola", "Graficul unei functii de gradul al doilea.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("asimptota", "Dreapta de care graficul unei functii se apropie indefinit.", "matematica", Difficulty.Hard, SubjectSlug: "math"),
        new("probabilitate", "Masura sanselor ca un eveniment sa se produca.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("permutare", "Aranjare a tuturor elementelor unei multimi intr-o anumita ordine.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("combinare", "Selectare de elemente fara a tine cont de ordine.", "matematica", Difficulty.Medium, SubjectSlug: "math"),
        new("polinom", "Expresie algebrica formata din suma unor monoame.", "matematica", Difficulty.Easy, SubjectSlug: "math"),
        new("radical", "Simbol sau operatie folosita pentru extragerea radacinii.", "matematica", Difficulty.Easy, SubjectSlug: "math"),
        new("teorema", "Afirmatie matematica demonstrata riguros.", "matematica", Difficulty.Easy, SubjectSlug: "math"),

        new("algoritm", "Sir finit de pasi clari pentru rezolvarea unei probleme.", "informatica", Difficulty.Easy, SubjectSlug: "informatics"),
        new("variabila", "Zona de memorie identificata prin nume, care pastreaza o valoare.", "informatica", Difficulty.Easy, SubjectSlug: "informatics"),
        new("compilator", "Program care traduce codul sursa intr-o forma executabila.", "informatica", Difficulty.Medium, SubjectSlug: "informatics"),
        new("recursivitate", "Tehnica prin care o functie se apeleaza pe ea insasi.", "informatica", Difficulty.Hard, SubjectSlug: "informatics"),
        new("stiva", "Structura de date de tip LIFO.", "informatica", Difficulty.Medium, SubjectSlug: "informatics"),
        new("coada", "Structura de date de tip FIFO.", "informatica", Difficulty.Medium, SubjectSlug: "informatics"),
        new("graf", "Structura formata din noduri si muchii.", "informatica", Difficulty.Medium, SubjectSlug: "informatics"),
        new("arbore", "Graf conex fara cicluri.", "informatica", Difficulty.Medium, SubjectSlug: "informatics"),
        new("sortare", "Procesul de ordonare a elementelor dupa un criteriu.", "informatica", Difficulty.Easy, SubjectSlug: "informatics"),
        new("cautare", "Procesul de gasire a unui element intr-o colectie.", "informatica", Difficulty.Easy, SubjectSlug: "informatics"),
        new("pointer", "Variabila care retine adresa unei zone de memorie.", "informatica", Difficulty.Hard, SubjectSlug: "informatics"),
        new("clasa", "Sablon pentru obiecte in programarea orientata pe obiecte.", "informatica", Difficulty.Medium, SubjectSlug: "informatics"),
        new("obiect", "Instanta concreta a unei clase.", "informatica", Difficulty.Easy, SubjectSlug: "informatics"),
        new("binar", "Sistem de numeratie care foloseste cifrele zero si unu.", "informatica", Difficulty.Easy, SubjectSlug: "informatics"),

        new("dacii", "Populatia antica din spatiul carpato-danubiano-pontic.", "istorie", Difficulty.Easy, SubjectSlug: "history"),
        new("romanizare", "Procesul de preluare a limbii si culturii romane.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("unire", "Act politic prin care provincii sau state se reunesc.", "istorie", Difficulty.Easy, SubjectSlug: "history"),
        new("revolutie", "Schimbare politica si sociala brusca, de obicei violenta.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("monarhie", "Forma de guvernare in care conducatorul este rege sau imparat.", "istorie", Difficulty.Easy, SubjectSlug: "history"),
        new("republica", "Forma de guvernare in care seful statului este ales.", "istorie", Difficulty.Easy, SubjectSlug: "history"),
        new("constitutie", "Legea fundamentala care organizeaza statul.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("independenta", "Statutul politic al unui stat suveran.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("democratie", "Regim politic bazat pe participarea cetatenilor.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("dictatura", "Regim in care puterea este concentrata autoritar.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("feudalism", "Organizare sociala medievala bazata pe relatii de vasalitate.", "istorie", Difficulty.Hard, SubjectSlug: "history"),
        new("umanism", "Curent cultural renascentist centrat pe valoarea omului.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("renastere", "Epoca de reinnoire culturala europeana dupa Evul Mediu.", "istorie", Difficulty.Medium, SubjectSlug: "history"),
        new("razboi", "Conflict armat intre state sau grupuri.", "istorie", Difficulty.Easy, SubjectSlug: "history"),

        new("forta", "Marime vectoriala care poate modifica starea de miscare a unui corp.", "fizica", Difficulty.Easy, SubjectSlug: "physics"),
        new("energie", "Capacitatea unui sistem de a efectua lucru mecanic.", "fizica", Difficulty.Easy, SubjectSlug: "physics"),
        new("putere", "Lucrul mecanic efectuat in unitatea de timp.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("viteza", "Raportul dintre distanta parcursa si timp.", "fizica", Difficulty.Easy, SubjectSlug: "physics"),
        new("acceleratie", "Variatia vitezei in unitatea de timp.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("presiune", "Forta exercitata pe unitatea de suprafata.", "fizica", Difficulty.Easy, SubjectSlug: "physics"),
        new("densitate", "Raportul dintre masa si volum.", "fizica", Difficulty.Easy, SubjectSlug: "physics"),
        new("curent", "Miscarea ordonata a sarcinilor electrice.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("tensiune", "Diferenta de potential electric dintre doua puncte.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("rezistenta", "Marime care se opune trecerii curentului electric.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("magnetism", "Fenomen asociat campurilor magnetice.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("frecare", "Forta care se opune miscarii relative dintre suprafete.", "fizica", Difficulty.Easy, SubjectSlug: "physics"),
        new("inertie", "Proprietatea corpurilor de a-si pastra starea de miscare sau repaus.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),
        new("lentila", "Corp transparent care deviaza razele de lumina.", "fizica", Difficulty.Medium, SubjectSlug: "physics"),

        new("relief", "Totalitatea formelor suprafetei terestre.", "geografie", Difficulty.Easy, SubjectSlug: "geography"),
        new("clima", "Regimul multianual al vremii intr-o regiune.", "geografie", Difficulty.Easy, SubjectSlug: "geography"),
        new("latitudine", "Distanta unghiulara fata de Ecuator.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("longitudine", "Distanta unghiulara fata de meridianul Greenwich.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("meridian", "Semicerc imaginar care uneste polii geografici.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("paralela", "Cerc imaginar paralel cu Ecuatorul.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("delta", "Forma de relief la varsarea unui fluviu in mare sau lac.", "geografie", Difficulty.Easy, SubjectSlug: "geography"),
        new("podis", "Forma de relief inalta, relativ neteda.", "geografie", Difficulty.Easy, SubjectSlug: "geography"),
        new("campie", "Forma de relief joasa si neteda.", "geografie", Difficulty.Easy, SubjectSlug: "geography"),
        new("depresiune", "Zona mai joasa inconjurata de forme de relief mai inalte.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("hidrografie", "Totalitatea apelor dintr-un teritoriu.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("urbanizare", "Cresterea rolului si numarului oraselor.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("migratie", "Deplasarea populatiei dintr-o regiune in alta.", "geografie", Difficulty.Medium, SubjectSlug: "geography"),
        new("continent", "Mare intindere de uscat delimitata conventional.", "geografie", Difficulty.Easy, SubjectSlug: "geography"),

        new("celula", "Unitatea structurala si functionala a organismelor vii.", "biologie", Difficulty.Easy, SubjectSlug: "biology"),
        new("tesut", "Grup de celule asemanatoare cu aceeasi functie.", "biologie", Difficulty.Easy, SubjectSlug: "biology"),
        new("organ", "Structura formata din tesuturi care indeplineste o functie.", "biologie", Difficulty.Easy, SubjectSlug: "biology"),
        new("sistem", "Ansamblu de organe care colaboreaza pentru o functie vitala.", "biologie", Difficulty.Easy, SubjectSlug: "biology"),
        new("fotosinteza", "Proces prin care plantele produc substante organice folosind lumina.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("respiratie", "Proces celular prin care se elibereaza energie.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("enzima", "Proteina ce accelereaza reactiile biochimice.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("gena", "Segment de ADN care contine informatie ereditara.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("cromozom", "Structura celulara care contine ADN.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("mitoza", "Diviziune celulara care produce doua celule identice.", "biologie", Difficulty.Hard, SubjectSlug: "biology"),
        new("meioza", "Diviziune celulara care produce gameti.", "biologie", Difficulty.Hard, SubjectSlug: "biology"),
        new("ecosistem", "Ansamblu format din biocenoza si biotop.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("biotop", "Mediul fizic al unei comunitati de organisme.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),
        new("evolutie", "Schimbarea populatiilor de organisme de-a lungul generatiilor.", "biologie", Difficulty.Medium, SubjectSlug: "biology"),

        new("atom", "Cea mai mica particula care pastreaza proprietatile unui element.", "chimie", Difficulty.Easy, SubjectSlug: "chemistry"),
        new("molecula", "Grup de atomi legati chimic.", "chimie", Difficulty.Easy, SubjectSlug: "chemistry"),
        new("ion", "Atom sau grup de atomi cu sarcina electrica.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("izotop", "Atom al aceluiasi element cu numar diferit de neutroni.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("valenta", "Capacitatea unui atom de a forma legaturi chimice.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("acid", "Substanta care poate ceda protoni in solutie.", "chimie", Difficulty.Easy, SubjectSlug: "chemistry"),
        new("baza", "Substanta care poate accepta protoni sau elibera ioni hidroxid.", "chimie", Difficulty.Easy, SubjectSlug: "chemistry"),
        new("sare", "Compus ionic rezultat adesea din reactia acidului cu baza.", "chimie", Difficulty.Easy, SubjectSlug: "chemistry"),
        new("oxidare", "Proces chimic in care o specie pierde electroni.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("reducere", "Proces chimic in care o specie castiga electroni.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("catalizator", "Substanta care mareste viteza unei reactii fara a se consuma.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("solutie", "Amestec omogen format din dizolvant si solvat.", "chimie", Difficulty.Easy, SubjectSlug: "chemistry"),
        new("concentratie", "Cantitatea de substanta dizolvata intr-o solutie.", "chimie", Difficulty.Medium, SubjectSlug: "chemistry"),
        new("hidroliza", "Reactie chimica in care apa descompune un compus.", "chimie", Difficulty.Hard, SubjectSlug: "chemistry"),

        new("credinta", "Atitudine de incredere si adeziune fata de valori religioase.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("rugaciune", "Forma de comunicare spirituala cu Dumnezeu.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("evanghelie", "Text crestin care prezinta viata si invatatura lui Iisus.", "religie", Difficulty.Medium, SubjectSlug: "religion"),
        new("apostol", "Ucenic trimis sa raspandeasca invatatura crestina.", "religie", Difficulty.Medium, SubjectSlug: "religion"),
        new("parabola", "Povestire cu sens moral sau religios.", "religie", Difficulty.Medium, SubjectSlug: "religion"),
        new("virtute", "Calitate morala orientata spre bine.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("porunca", "Indemn sau regula religioasa cu valoare morala.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("botez", "Taina crestina a intrarii in comunitatea Bisericii.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("liturghie", "Slujba centrala a cultului crestin.", "religie", Difficulty.Medium, SubjectSlug: "religion"),
        new("icoana", "Imagine sacra folosita in traditia crestina.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("pelerinaj", "Calatorie la un loc sfant.", "religie", Difficulty.Medium, SubjectSlug: "religion"),
        new("post", "Practica de cumpatare si pregatire spirituala.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("iertare", "Renuntare la resentiment si impacare morala.", "religie", Difficulty.Easy, SubjectSlug: "religion"),
        new("comuniune", "Legatura spirituala intre persoane si comunitate.", "religie", Difficulty.Medium, SubjectSlug: "religion"),

        new("piata", "Loc abstract unde se intalnesc cererea si oferta.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("cerere", "Cantitatea pe care consumatorii doresc sa o cumpere.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("oferta", "Cantitatea pe care producatorii doresc sa o vanda.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("pret", "Suma platita pentru un bun sau serviciu.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("profit", "Diferenta pozitiva dintre venituri si costuri.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("cost", "Valoarea resurselor consumate pentru productie.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("buget", "Plan al veniturilor si cheltuielilor.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("inflatie", "Crestere generala si persistenta a preturilor.", "economie", Difficulty.Medium, SubjectSlug: "economy"),
        new("moneda", "Instrument acceptat pentru plati si economisire.", "economie", Difficulty.Easy, SubjectSlug: "economy"),
        new("credit", "Imprumut acordat cu obligatia rambursarii.", "economie", Difficulty.Medium, SubjectSlug: "economy"),
        new("dobanda", "Pretul folosirii banilor imprumutati.", "economie", Difficulty.Medium, SubjectSlug: "economy"),
        new("investitie", "Folosirea resurselor pentru castig viitor.", "economie", Difficulty.Medium, SubjectSlug: "economy"),
        new("capital", "Resursa folosita pentru producerea altor bunuri.", "economie", Difficulty.Medium, SubjectSlug: "economy"),
        new("salariu", "Venit primit pentru munca prestata.", "economie", Difficulty.Easy, SubjectSlug: "economy"),

        new("noun", "In English grammar, word that names a person, place, thing, or idea.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("verb", "In English grammar, word that expresses action or state.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("adjective", "Word that describes a noun in English.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("adverb", "Word that modifies a verb, adjective, or another adverb.", "engleza", Difficulty.Medium, SubjectSlug: "english"),
        new("pronoun", "Word used instead of a noun.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("preposition", "Word that shows relation such as place, time, or direction.", "engleza", Difficulty.Medium, SubjectSlug: "english"),
        new("conjunction", "Word that connects words, phrases, or clauses.", "engleza", Difficulty.Medium, SubjectSlug: "english"),
        new("tense", "Grammar form that places an action in time.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("passive", "Voice in which the subject receives the action.", "engleza", Difficulty.Medium, SubjectSlug: "english"),
        new("reported", "Type of speech used to tell what someone said indirectly.", "engleza", Difficulty.Medium, SubjectSlug: "english"),
        new("conditional", "Structure used for possible or hypothetical situations.", "engleza", Difficulty.Hard, SubjectSlug: "english"),
        new("article", "Word such as a, an, or the.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("plural", "Form that indicates more than one.", "engleza", Difficulty.Easy, SubjectSlug: "english"),
        new("synonym", "Word with a similar meaning to another word.", "engleza", Difficulty.Easy, SubjectSlug: "english"),

        new("nom", "In gramatica franceza, cuvant care denumeste fiinte, obiecte sau idei.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("verbe", "In franceza, partea de vorbire care exprima actiunea sau starea.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("adjectif", "Cuvant francez care exprima insusirea unui substantiv.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("adverbe", "Cuvant francez care modifica un verb, adjectiv sau adverb.", "franceza", Difficulty.Medium, SubjectSlug: "french"),
        new("pronom", "Cuvant francez care inlocuieste un substantiv.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("article", "Determinant francez asezat de obicei inaintea substantivului.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("present", "Timp verbal francez al actiunii actuale.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("passe", "Forma temporala franceza care indica trecutul.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("futur", "Timp verbal francez care indica actiuni viitoare.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("accord", "Potrivirea de gen si numar in gramatica franceza.", "franceza", Difficulty.Medium, SubjectSlug: "french"),
        new("liaison", "Pronuntarea consoanei finale inaintea unui cuvant cu vocala.", "franceza", Difficulty.Hard, SubjectSlug: "french"),
        new("negation", "Structura franceza folosita pentru a nega o propozitie.", "franceza", Difficulty.Medium, SubjectSlug: "french"),
        new("question", "Constructie franceza folosita pentru a cere informatie.", "franceza", Difficulty.Easy, SubjectSlug: "french"),
        new("accent", "Semn grafic francez care modifica pronuntia sau sensul.", "franceza", Difficulty.Easy, SubjectSlug: "french"),

        new("nomen", "In gramatica germana, cuvant care denumeste fiinte, lucruri sau idei.", "germana", Difficulty.Easy, SubjectSlug: "german"),
        new("verb", "In germana, partea de vorbire care exprima actiunea sau starea.", "germana", Difficulty.Easy, SubjectSlug: "german"),
        new("adjektiv", "Cuvant german care descrie un substantiv.", "germana", Difficulty.Easy, SubjectSlug: "german"),
        new("artikel", "Cuvant german precum der, die sau das.", "germana", Difficulty.Easy, SubjectSlug: "german"),
        new("pronomen", "Cuvant german care tine locul unui substantiv.", "germana", Difficulty.Easy, SubjectSlug: "german"),
        new("kasus", "Categorie gramaticala germana precum nominativ sau acuzativ.", "germana", Difficulty.Medium, SubjectSlug: "german"),
        new("nominativ", "Caz german folosit de obicei pentru subiect.", "germana", Difficulty.Medium, SubjectSlug: "german"),
        new("akkusativ", "Caz german folosit frecvent pentru complementul direct.", "germana", Difficulty.Medium, SubjectSlug: "german"),
        new("dativ", "Caz german care indica adesea destinatarul actiunii.", "germana", Difficulty.Medium, SubjectSlug: "german"),
        new("genitiv", "Caz german care exprima posesia.", "germana", Difficulty.Hard, SubjectSlug: "german"),
        new("perfekt", "Timp compus german folosit pentru actiuni trecute.", "germana", Difficulty.Medium, SubjectSlug: "german"),
        new("prasens", "Timp verbal german al prezentului.", "germana", Difficulty.Easy, SubjectSlug: "german"),
        new("trennbar", "Tip de verb german cu prefix separabil.", "germana", Difficulty.Hard, SubjectSlug: "german"),
        new("satz", "Propozitie sau enunt in limba germana.", "germana", Difficulty.Easy, SubjectSlug: "german")
    ];
}
