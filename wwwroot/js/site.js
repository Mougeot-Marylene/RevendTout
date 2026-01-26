// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


// Array.from => on transforme "document.getElementsByClassName("btnSupp")" en tableau car pour faire un foreach on a besoin que ce soit un tableau
// (document.getElementsByClassName => renvoie une collection qui contien tous les boutons / liens qui sont sur la page 
Array.from(document.getElementsByClassName("btnSuppr")).forEach(lien => {

    // quand on fait un click sur le bouton ça lance une fonction JavaScript, qui affiche une petite popup "confirme"
    // addEventListener => ecouteur d'evenement
    lien.addEventListener("click", function (e) {
        // retourne un boolen et window.Confirm demande au navigateur d'afficher une popup avec un message, et ça attend que l'utilisateur confirme ou annule       
        let confirmeDelete = confirm("Êtes-vous sûr de vouloir supprimer cette catégorie ?");
        // si il confirme => true(par defaut) (on va vouloir supprimer) , si il annule => false (on ne supprime pas)
        if (!confirmeDelete) {
            e.preventDefault();// le comportement par defaut de l'evenement je ne le fait pas , fais donc false 
        }
    })
});

Array.from(document.getElementsByClassName("btn_archive")).forEach(lien => {

    // quand on fait un click sur le bouton ça lance une fonction JavaScript, qui affiche une petite popup "confirme"
    // addEventListener => ecouteur d'evenement
    lien.addEventListener("click", function (e) {
        // retourne un boolen et window.Confirm demande au navigateur d'afficher une popup avec un message, et ça attend que l'utilisateur confirme ou annule       
        let confirmeArchive = confirm("Êtes-vous sûr de vouloir archiver le produit ?");
        // si il confirme => true(par defaut) (on va vouloir supprimer) , si il annule => false (on ne supprime pas)
        if (!confirmeArchive) {
            e.preventDefault();// le comportement par defaut de l'evenement je ne le fait pas , fais donc false 
        }
    })
});


async function DateLivraison(zipcode) {
    console.log("Code postal :", zipcode);
    const url = "https://api-filrouge.2isa.eu/api/v1/shippingaddress?zipcode=" + zipcode;
    try {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`Response status: ${response.status}`);
        }
        const result = await response.json();
        console.log("distance : " + result.distanceKM + "KM");

        let nbKm = 0;
        let nbJourPrepa = 1; // 1 jour pour la préparation
        let jour = 0; // nombre jour final
        // 30 nb de km par jour
        nbKm = result.distanceKM / 30;
        console.log("nbKm par jour :", nbKm);

        // je transforme en chaine de caractère pour recup le premier nombre et l'arrondir au nombre supp
        let kmFirst = nbKm.toString();
        
        const index = 0;
        // je recup le premier caractère (index = 0)
        let kmArr = kmFirst.at(index)

        //je convertie le sting en int
        let parseNb = parseInt(kmArr);

        // je calcul le 1er nombre (1 jour de prepa, + le 1er num de distance + 1 (pour arrondir au supp))
        jour = nbJourPrepa + (parseNb + 1);
        console.log(`Nombre de jour ${jour}`);

         let ajoutFichier = document.getElementById('jour').textContent = jour + " jours";

        
    } catch (error) {
        console.error(error.Message);
    }
}
